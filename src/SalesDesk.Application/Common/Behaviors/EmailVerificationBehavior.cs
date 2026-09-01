using MediatR;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Common.Behaviors;

/// <summary>
/// TASK-030 AC3: blocks an authenticated-but-unverified caller from every mutation
/// (every request whose type follows this codebase's own <c>*Command</c> naming
/// convention — see RegisterCommand, LoginCommand, GetCurrentUserQuery for the
/// pattern this leans on) except the handful that must stay reachable precisely
/// because the caller isn't verified yet: signing in, requesting/consuming a
/// password reset, and requesting/consuming the verification link itself.
///
/// Registered after <see cref="ValidationBehavior{TRequest,TResponse}"/> in
/// DependencyInjection.AddApplication, so a malformed request still comes back as
/// an ordinary 400 rather than this behavior's 403 — this only runs once the
/// request has already been judged well-formed.
///
/// Queries stay unblocked entirely: an unverified user can still see their own
/// data (e.g. GetCurrentUserQuery, which the frontend needs to even know it must
/// show the verification banner) — only state-changing actions are gated, per the
/// AC's "protected workspace routes or performing mutation actions" wording.
/// </summary>
public sealed class EmailVerificationBehavior<TRequest, TResponse>(ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly HashSet<string> ExemptCommands = new(StringComparer.Ordinal)
    {
        nameof(RegisterCommand),
        nameof(LoginCommand),
        nameof(ForgotPasswordCommand),
        nameof(ResetPasswordCommand),
        nameof(VerifyEmailCommand),
        nameof(ResendVerificationEmailCommand)
    };

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        var isGatedMutation = requestName.EndsWith("Command", StringComparison.Ordinal) && !ExemptCommands.Contains(requestName);

        if (isGatedMutation && currentUser.IsAuthenticated && !currentUser.IsEmailVerified)
        {
            throw new ForbiddenException("Please verify your email address before performing this action. Check your inbox for the verification link, or request a new one.");
        }

        return next();
    }
}
