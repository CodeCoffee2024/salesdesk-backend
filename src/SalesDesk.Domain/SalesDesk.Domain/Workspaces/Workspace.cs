using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Workspaces;

/// <summary>
/// The business/studio a SalesDesk account represents. Documents are issued "from"
/// this profile — name, tagline, address, email and logo appear on every quote and
/// invoice.
/// </summary>
public sealed class Workspace : Entity
{
    public string Name { get; private set; }

    public string? Tagline { get; private set; }

    public string? Address { get; private set; }

    public string Email { get; private set; }

    public string? LogoUrl { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 code (e.g. "US", "DE", "PH") for this workspace's primary country of operation — drives tax-label inference and the default target country for new documents (TASK-029).</summary>
    public string Country { get; private set; }

    /// <summary>ISO 4217 code (e.g. "USD", "EUR", "PHP") documents default to unless overridden per-document, and the currency dashboard totals are normalized into (TASK-029).</summary>
    public string DefaultCurrency { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Maximum documents this workspace may issue. Null means unlimited.</summary>
    public int? DocumentQuota { get; private set; }

    /// <summary>TASK-031: Free for every workspace unless upgraded — see <see cref="GrantEarlyBirdPro"/>.</summary>
    public SubscriptionTier SubscriptionTier { get; private set; }

    /// <summary>When a paid <see cref="SubscriptionTier"/> lapses. Null for Free, and (today) also null for a paid grant that isn't time-boxed — the early-bird promo is the only path to a paid tier so far, and always sets this.</summary>
    public DateTimeOffset? SubscriptionEndDate { get; private set; }

    /// <summary>True for one of the first 100 eligible accounts registered — drives the "Early 100 Free Year" badge on /settings/billing. Distinct from SubscriptionTier because a future non-promo Premium upgrade shouldn't retroactively claim this badge.</summary>
    public bool IsEarlyBirdPromo { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Workspace()
    {
        Name = string.Empty;
        Email = string.Empty;
        Country = "US";
        DefaultCurrency = "USD";
    }

    public Workspace(
        string name,
        string email,
        string? tagline = null,
        string? address = null,
        string? logoUrl = null,
        int? documentQuota = 100,
        string country = "US",
        string defaultCurrency = "USD")
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Tagline = tagline;
        Address = address;
        LogoUrl = logoUrl;
        Country = Guard.AgainstInvalidIsoCode(country, 2, nameof(country));
        DefaultCurrency = Guard.AgainstInvalidIsoCode(defaultCurrency, 3, nameof(defaultCurrency));
        IsActive = true;
        DocumentQuota = documentQuota;
        SubscriptionTier = SubscriptionTier.Free;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string name, string email, string? tagline, string? address, string? logoUrl, string country, string defaultCurrency)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Tagline = tagline;
        Address = address;
        LogoUrl = logoUrl;
        Country = Guard.AgainstInvalidIsoCode(country, 2, nameof(country));
        DefaultCurrency = Guard.AgainstInvalidIsoCode(defaultCurrency, 3, nameof(defaultCurrency));
    }

    /// <summary>Blocks every user of this workspace from signing in — see LoginCommandHandler.</summary>
    public void Suspend() => IsActive = false;

    public void Activate() => IsActive = true;

    public void SetDocumentQuota(int? documentQuota)
    {
        if (documentQuota is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentQuota), documentQuota, "Document quota cannot be negative.");
        }

        DocumentQuota = documentQuota;
    }

    /// <summary>
    /// TASK-031: grants the "Early 100 Free Year" promo — PRO tier with a $0.00
    /// billing override (there's no billing/invoicing of the workspace itself
    /// yet, so "billing override" just means SubscriptionTier reads Pro without
    /// any charge ever being raised for it) expiring exactly 365 days from
    /// <paramref name="registeredAtUtc"/>. Called from RegisterCommandHandler
    /// only immediately after IApplicationDbContext.TryReserveEarlyBirdPromoSlotAsync
    /// has confirmed this registration is the 100th or earlier — never speculatively.
    /// </summary>
    public void GrantEarlyBirdPro(DateTimeOffset registeredAtUtc)
    {
        SubscriptionTier = SubscriptionTier.Pro;
        SubscriptionEndDate = registeredAtUtc.AddDays(365);
        IsEarlyBirdPromo = true;
    }
}
