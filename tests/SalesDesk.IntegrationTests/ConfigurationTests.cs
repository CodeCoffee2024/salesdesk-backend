using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SalesDesk.IntegrationTests;

/// <summary>
/// Regression guard: a malformed appsettings.json compiles cleanly but kills the
/// host at startup. Building the factory here fails loudly if config is invalid.
/// </summary>
public class ConfigurationTests : IClassFixture<SalesDeskApiFactory>
{
    private readonly SalesDeskApiFactory _factory;

    public ConfigurationTests(SalesDeskApiFactory factory)
        => _factory = factory;

    [Fact]
    public void Host_builds_with_valid_configuration()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        configuration.Should().NotBeNull();
    }

    [Fact]
    public void SalesDesk_connection_string_is_configured()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        configuration.GetConnectionString("SalesDesk")
            .Should().NotBeNullOrWhiteSpace(
                "the API needs a PostgreSQL connection string to reach the database");
    }
}
