using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SalesDesk.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c>/<c>dotnet ef database update</c> build a
/// <see cref="SalesDeskDbContext"/> at design time without starting the API host.
/// Only used by EF Core tooling — never resolved at runtime, so it deliberately
/// bypasses <see cref="DependencyInjection.AddInfrastructure"/> and reads the
/// connection string directly: the local Postgres default from
/// docker-compose.yml, or the ConnectionStrings__SalesDesk environment variable
/// when set (e.g. to point migrations at a different environment).
/// </summary>
public sealed class SalesDeskDbContextFactory : IDesignTimeDbContextFactory<SalesDeskDbContext>
{
    private const string LocalDevConnectionString =
        "Host=localhost;Port=5432;Database=salesdesk;Username=salesdesk;Password=salesdesk";

    public SalesDeskDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__SalesDesk") ?? LocalDevConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<SalesDeskDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(SalesDeskDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();

        return new SalesDeskDbContext(optionsBuilder.Options);
    }
}