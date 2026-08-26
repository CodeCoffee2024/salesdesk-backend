using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Users;

namespace SalesDesk.IntegrationTests;

/// <summary>
/// End-to-end check that an invalid request comes back as a real, standardized
/// ProblemDetails response through the whole pipeline (controller model binding →
/// MediatR ValidationBehavior → GlobalExceptionHandler) — not just that the
/// FluentValidation rule itself fires, which the Application layer's own unit
/// tests already cover. Stays DB-free: validation runs before any handler touches
/// the database, so this doesn't need a live PostgreSQL instance.
/// </summary>
public class ValidationErrorResponseTests : IClassFixture<SalesDeskApiFactory>
{
    private readonly SalesDeskApiFactory _factory;

    public ValidationErrorResponseTests(SalesDeskApiFactory factory)
        => _factory = factory;

    [Fact]
    public async Task Creating_a_customer_with_a_malformed_email_returns_a_validation_problem_details_response()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueTestToken());

        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Maya Chen",
            company = "Northstar Studio",
            email = "not-an-email",
            phone = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainKey("Email");
    }

    // Every controller requires authentication by default (TASK-015 AC4), so
    // exercising a validation failure past that gate needs a real, validly-signed
    // token — minted via the host's own registered ITokenService.
    private string IssueTestToken()
    {
        var tokenService = _factory.Services.GetRequiredService<ITokenService>();
        var user = new User("test@northline.studio", "unused-hash", "Test User", Role.WorkspaceAdmin, Guid.NewGuid());

        return tokenService.IssueToken(user).Value;
    }
}
