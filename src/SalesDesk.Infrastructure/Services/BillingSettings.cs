using Microsoft.Extensions.Configuration;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>Reads the Billing: and GCash: config sections (see DependencyInjection) — see IBillingSettings for why this lives in Infrastructure, not Application.</summary>
public sealed class BillingSettings(IConfiguration configuration) : IBillingSettings
{
    public string? AdminNotificationEmail => configuration["Billing:AdminNotificationEmail"];

    public string? AccountName => configuration["GCash:AccountName"];

    public string? MobileNumber => configuration["GCash:MobileNumber"];

    public string? QrCodeUrl => configuration["GCash:QrCodeUrl"];
}
