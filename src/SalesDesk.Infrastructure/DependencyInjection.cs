using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Infrastructure.BackgroundServices;
using SalesDesk.Infrastructure.Persistence;
using SalesDesk.Infrastructure.Services;

namespace SalesDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SalesDesk");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'SalesDesk' is not configured. Set ConnectionStrings:SalesDesk in appsettings " +
                "or the ConnectionStrings__SalesDesk environment variable.");
        }

        services.AddDbContext<SalesDeskDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(SalesDeskDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<SalesDeskDbContext>());
        services.AddSingleton<IDateTime, SystemDateTime>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        // Automated reminders (TASK-025). No transactional-email provider is
        // configured yet, so IEmailSender logs instead of sending — see
        // LogEmailSender and docs/research/TASK-DEPLOYMENT.md. IPublicLinkBuilder
        // needs the deployed frontend's own base URL to build a `/view/{token}`
        // link from the backend; App:FrontendBaseUrl is intentionally allowed to be
        // empty (falls back to a relative link) rather than throwing at startup
        // like Jwt:Secret does, since the reminder engine is opt-in per workspace
        // and shouldn't block boot in an environment that hasn't set it yet.
        services.AddSingleton<IEmailSender, LogEmailSender>();
        services.AddSingleton<IPublicLinkBuilder>(new PublicLinkBuilder(configuration["App:FrontendBaseUrl"] ?? string.Empty));
        services.AddHostedService<ReminderDispatchHostedService>();

        return services;
    }
}
