using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SalesDesk.IntegrationTests;

/// <summary>
/// Boots the API host under the "Testing" environment instead of "Development", so
/// Program.cs's Development-only auto-migrate-and-seed step — which needs a live,
/// reachable PostgreSQL database — doesn't run just to build the host for a
/// config/DI smoke test. A valid (but not necessarily reachable) connection string
/// is still required, since <c>AddInfrastructure</c> validates one is configured
/// regardless of environment.
/// </summary>
public sealed class SalesDeskApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        base.ConfigureWebHost(builder);
    }
}
