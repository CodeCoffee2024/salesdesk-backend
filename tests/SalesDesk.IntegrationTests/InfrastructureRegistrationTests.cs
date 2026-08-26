using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesDesk.Infrastructure.Persistence;

namespace SalesDesk.IntegrationTests;

/// <summary>
/// Regression guard: catches a broken <c>AddInfrastructure</c> DI registration (a
/// missing service, a wrong lifetime, an options delegate that throws) without
/// needing a reachable PostgreSQL instance — constructing a DbContext doesn't open
/// a connection, so this stays a fast, DB-less smoke test.
/// </summary>
public class InfrastructureRegistrationTests : IClassFixture<SalesDeskApiFactory>
{
    private readonly SalesDeskApiFactory _factory;

    public InfrastructureRegistrationTests(SalesDeskApiFactory factory)
        => _factory = factory;

    [Fact]
    public void SalesDeskDbContext_is_resolvable_from_the_host()
    {
        using var scope = _factory.Services.CreateScope();

        var act = scope.ServiceProvider.GetRequiredService<SalesDeskDbContext>;

        act.Should().NotThrow();
    }

    [Fact]
    public void SalesDeskDbContext_is_registered_with_a_scoped_lifetime()
    {
        using var scopeOne = _factory.Services.CreateScope();
        using var scopeTwo = _factory.Services.CreateScope();

        var firstInScopeOne = scopeOne.ServiceProvider.GetRequiredService<SalesDeskDbContext>();
        var secondInScopeOne = scopeOne.ServiceProvider.GetRequiredService<SalesDeskDbContext>();
        var firstInScopeTwo = scopeTwo.ServiceProvider.GetRequiredService<SalesDeskDbContext>();

        secondInScopeOne.Should().BeSameAs(firstInScopeOne, "AddDbContext registers a scoped lifetime by default");
        firstInScopeTwo.Should().NotBeSameAs(firstInScopeOne, "each scope should get its own DbContext instance");
    }
}
