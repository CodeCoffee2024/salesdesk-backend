using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Users;

namespace SalesDesk.IntegrationTests;

/// <summary>
/// End-to-end check that TASK-016's CanManage/CanDelete policies actually gate the
/// controller actions, through the real ASP.NET Core authorization middleware — not
/// just that the policy is registered. Stays DB-free like ValidationErrorResponseTests:
/// a 403 is produced by the authorization middleware before any handler runs, and a
/// "policy allowed it through" assertion is proven via the FluentValidation failure
/// path (400), which also runs before the handler touches the database.
/// </summary>
public class AuthorizationPolicyTests : IClassFixture<SalesDeskApiFactory>
{
    private readonly SalesDeskApiFactory _factory;

    public AuthorizationPolicyTests(SalesDeskApiFactory factory)
        => _factory = factory;

    [Fact]
    public async Task Viewer_creating_a_customer_is_forbidden()
    {
        var client = AuthenticatedClient(Role.Viewer);

        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Maya Chen",
            company = "Northstar Studio",
            email = "maya@northstar.studio",
            phone = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SalesManager_deleting_a_customer_is_forbidden()
    {
        var client = AuthenticatedClient(Role.SalesManager);

        var response = await client.DeleteAsync($"/api/customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SalesManager_creating_a_customer_passes_authorization_and_reaches_validation()
    {
        var client = AuthenticatedClient(Role.SalesManager);

        // Malformed email: CanManage lets a SalesManager through to the handler
        // pipeline, so this fails FluentValidation (400) rather than the
        // authorization middleware (403) — proving the policy allowed the request,
        // without needing a live database for a full round-trip.
        var response = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Maya Chen",
            company = "Northstar Studio",
            email = "not-an-email",
            phone = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WorkspaceAdmin_deleting_a_customer_is_not_blocked_by_authorization()
    {
        var client = AuthenticatedClient(Role.WorkspaceAdmin);

        var response = await client.DeleteAsync($"/api/customers/{Guid.NewGuid()}");

        // No live database in this test host, so a policy-passing delete can't be
        // asserted all the way to 204 here — DeleteCustomerCommandHandler needs a
        // real connection to look the customer up. The point of this test is only
        // that CanDelete grants WorkspaceAdmin access (never 403), not the full
        // round-trip outcome.
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    private HttpClient AuthenticatedClient(Role role)
    {
        var client = _factory.CreateClient();

        var tokenService = _factory.Services.GetRequiredService<ITokenService>();
        var user = new User($"{role}@northline.studio", "unused-hash", $"Test {role}", role, Guid.NewGuid());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.IssueToken(user).Value);
        return client;
    }
}
