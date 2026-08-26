using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SalesDesk.Api.Tests;

/// <summary>
/// Smoke test proving the API host boots, controller routing is wired up, and
/// serves a request end to end. Replace/extend with real quotation/invoice
/// controller tests as they land.
/// </summary>
public class HealthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthControllerTests(WebApplicationFactory<Program> factory)
        => _factory = factory;

    [Fact]
    public async Task Get_api_health_returns_200()
    {
        // "Testing", not the default "Development": Program.cs only auto-migrates
        // and seeds the database in Development, which would otherwise make this
        // endpoint smoke test depend on a live, reachable PostgreSQL instance.
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"))
            .CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
