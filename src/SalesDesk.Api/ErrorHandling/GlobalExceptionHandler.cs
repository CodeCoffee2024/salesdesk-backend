using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;

namespace SalesDesk.Api.ErrorHandling;

/// <summary>
/// Single place every unhandled exception funnels through, turned into a
/// standardized <see cref="ProblemDetails"/> (RFC 9457) JSON response instead of a
/// raw 500 or an ad-hoc shape. Supersedes the earlier per-request BasicExceptionFilter
/// stopgap from Task-003.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, IServiceScopeFactory serviceScopeFactory) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            return await WriteValidationProblemAsync(httpContext, validationException, cancellationToken);
        }

        var (statusCode, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication failed"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            // A business-rule/state conflict (e.g. converting a quote that isn't
            // Accepted) — not a malformed request (400) and not a database-level
            // conflict (DbUpdateException), but still "the current state blocks
            // this action", which is exactly what 409 means.
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict with the current state"),
            DbUpdateException => (StatusCodes.Status409Conflict, "Conflict with existing data"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        // TASK-017 AC4: a genuinely unexpected failure is itself a critical system
        // event worth surfacing in the platform audit log.
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            await TryLogSystemErrorAsync(exception, cancellationToken);
        }

        httpContext.Response.StatusCode = statusCode;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message,
                Instance = httpContext.Request.Path
            }
        });

        return true;
    }

    // IAuditLogger is scoped, but this handler is registered as a singleton (via
    // AddExceptionHandler<T>), so it can't be constructor-injected directly without
    // becoming a captive dependency — resolve it from a fresh scope per call
    // instead. Any failure writing the audit entry is swallowed so it never masks
    // the original exception response.
    private async Task TryLogSystemErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            await auditLogger.LogAsync(AuditEventTypes.SystemError, exception.Message, workspaceId: null, userId: null, cancellationToken);
        }
        catch
        {
            // Logging the failure must never itself become the failure.
        }
    }

    private async ValueTask<bool> WriteValidationProblemAsync(
        HttpContext httpContext, ValidationException exception, CancellationToken cancellationToken)
    {
        const int statusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.StatusCode = statusCode;

        var errors = exception.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ValidationProblemDetails(errors)
            {
                Status = statusCode,
                Title = "One or more validation errors occurred",
                Instance = httpContext.Request.Path
            }
        });

        return true;
    }
}
