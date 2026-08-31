using System.Net.Http.Headers;
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

        // Automated reminders (TASK-025). IPublicLinkBuilder needs the deployed
        // frontend's own base URL to build a `/view/{token}` link from the backend;
        // App:FrontendBaseUrl is intentionally allowed to be empty (falls back to a
        // relative link) rather than throwing at startup like Jwt:Secret does, since
        // the reminder engine is opt-in per workspace and shouldn't block boot in an
        // environment that hasn't set it yet.
        services.AddSingleton<IPublicLinkBuilder>(new PublicLinkBuilder(configuration["App:FrontendBaseUrl"] ?? string.Empty));
        services.AddHostedService<ReminderDispatchHostedService>();

        // Email delivery only goes live once Resend:ApiKey is configured —
        // otherwise the reminder/forgot-password paths fall back to a log-only
        // sender rather than failing every send in an environment that hasn't
        // configured email credentials yet. See docs/research/TASK-DEPLOYMENT.md.
        var resendApiKey = configuration["Resend:ApiKey"];
        if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
            {
                client.BaseAddress = new Uri("https://api.resend.com/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resendApiKey);
            });
        }
        else
        {
            services.AddSingleton<IEmailSender, LogEmailSender>();
        }

        return services;
    }
}
