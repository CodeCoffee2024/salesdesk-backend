using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Billing;

/// <summary>One priced tier within a region's catalog (TASK-038).</summary>
public sealed record PricingTierDto(
    string Tier,
    string DisplayName,
    string Currency,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    /// <summary>Documents this tier may issue per calendar month. Null means unlimited.</summary>
    int? MonthlyDocumentLimit,
    /// <summary>Null means unlimited (Studio's multi-user RBAC).</summary>
    int? MaxUsers,
    List<string> Features);

public sealed record PricingCatalogDto(string Region, string Currency, List<PricingTierDto> Tiers);

/// <summary>
/// TASK-038: Purchasing Power Parity pricing — a Philippines-registered workspace
/// (Workspace.Country == "PH") sees PHP pricing sized for a local freelancer's
/// budget; every other workspace sees the USD global catalog. Static, not
/// database-backed, matching StaticRateCurrencyConversionService's precedent for
/// "product config that changes by a deploy, not a database write."
///
/// There's no server-side IP geolocation here (the platform doesn't have a
/// MaxMind/ip-api-style provider configured, and this is a Filipino-freelancer
/// product where a workspace already declares its own operating country in
/// Settings during onboarding — TASK-029) — that self-reported Country is the
/// region signal, not a live IP lookup. If real IP-based detection is added
/// later, it belongs at registration time as a suggested default for Country,
/// not as a second, separate signal this catalog would also need to reconcile.
/// </summary>
public static class PricingCatalog
{
    public static PricingCatalogDto ForCountry(string countryCode)
    {
        var isPh = string.Equals(countryCode, "PH", StringComparison.OrdinalIgnoreCase);
        return isPh ? PhCatalog : GlobalCatalog;
    }

    /// <summary>Documents a tier may issue per calendar month; null means unlimited. Region doesn't change this, only price, so either catalog's copy of the tier is equally correct here.</summary>
    public static int? MonthlyDocumentLimit(SubscriptionTier tier) =>
        GlobalCatalog.Tiers.Single(t => t.Tier == tier.ToString()).MonthlyDocumentLimit;

    private static readonly PricingCatalogDto PhCatalog = new(
        "PH",
        "PHP",
        [
            new PricingTierDto(
                "Free", "Free / Starter", "PHP", 0m, 0m, 5, 1,
                ["Up to 5 active documents/month", "1 user", "SalesDesk watermark on documents"]),
            new PricingTierDto(
                "Pro", "Pro Freelancer", "PHP", 199m, 1990m, null, 1,
                ["Unlimited documents", "Dynamic merge tags", "Native e-signatures", "PWA offline support", "Custom logo & branding"]),
            new PricingTierDto(
                "Studio", "Studio / Agency", "PHP", 599m, 5990m, null, null,
                ["Multi-user RBAC", "Custom domain client portal", "Automated payment reminders", "Client inquiry webhook integration"])
        ]);

    private static readonly PricingCatalogDto GlobalCatalog = new(
        "Global",
        "USD",
        [
            new PricingTierDto(
                "Free", "Free / Starter", "USD", 0m, 0m, 5, 1,
                ["Up to 5 active documents/month", "1 user", "SalesDesk watermark on documents"]),
            new PricingTierDto(
                "Pro", "Pro Freelancer", "USD", 9.99m, 99.99m, null, 1,
                ["Unlimited documents", "Dynamic merge tags", "Native e-signatures", "PWA offline support", "Custom logo & branding"]),
            new PricingTierDto(
                "Studio", "Studio / Agency", "USD", 29.99m, 299.99m, null, null,
                ["Multi-user RBAC", "Custom domain client portal", "Automated payment reminders", "Client inquiry webhook integration"])
        ]);
}
