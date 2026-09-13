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

    /// <summary>IANA time zone id (e.g. "Asia/Manila", "America/Los_Angeles") this workspace operates in. Every outgoing document/reminder email localizes its activity-timeline timestamps into this zone instead of raw UTC, so a client reads times the way the business itself would. Defaults to "UTC" for a workspace that hasn't set one.</summary>
    public string TimeZoneId { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Platform-admin override of this workspace's monthly document limit, adjustable
    /// via the Admin Workspaces console independent of <see cref="SubscriptionTier"/>
    /// (e.g. granting a specific Free-tier customer extra headroom, or capping a
    /// problem account below what its tier would normally allow). Null means no
    /// override — the effective limit is whatever <c>PricingCatalog.MonthlyDocumentLimit</c>
    /// says for this workspace's tier (see <c>CreateDocumentCommand</c>). Defaults to
    /// null for every new workspace: it used to default to 100 before subscription
    /// tiers existed, which silently raised every Free-tier workspace's real cap from
    /// tier's 5 to 100 once tier-based enforcement shipped (TASK-038) — see
    /// docs/feature/billing-plan-limits.md.
    /// </summary>
    public int? DocumentQuota { get; private set; }

    /// <summary>TASK-031: Free for every workspace unless upgraded — see <see cref="GrantEarlyBirdPro"/>.</summary>
    public SubscriptionTier SubscriptionTier { get; private set; }

    /// <summary>When a paid <see cref="SubscriptionTier"/> lapses. Null for Free, and (today) also null for a paid grant that isn't time-boxed — the early-bird promo is the only path to a paid tier so far, and always sets this.</summary>
    public DateTimeOffset? SubscriptionEndDate { get; private set; }

    /// <summary>True for one of the first 100 eligible accounts registered — drives the "Early 100 Free Year" badge on /settings/billing. Distinct from SubscriptionTier because a future non-promo Premium upgrade shouldn't retroactively claim this badge.</summary>
    public bool IsEarlyBirdPromo { get; private set; }

    /// <summary>True while this workspace is on its one-time 7-day Full Access trial (<see cref="StartFreeTrial"/>) — drives the "Free trial" badge on /settings/billing, distinct from a real paid subscription or the early-bird promo. Cleared the moment a real paid subscription activates (<see cref="ActivatePaidSubscription"/>), so a customer who upgrades mid-trial sees "paid", not "trial".</summary>
    public bool IsFreeTrial { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Workspace()
    {
        Name = string.Empty;
        Email = string.Empty;
        Country = "US";
        DefaultCurrency = "USD";
        TimeZoneId = "UTC";
    }

    public Workspace(
        string name,
        string email,
        string? tagline = null,
        string? address = null,
        string? logoUrl = null,
        int? documentQuota = null,
        string country = "US",
        string defaultCurrency = "USD",
        string timeZoneId = "UTC")
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Tagline = tagline;
        Address = address;
        LogoUrl = logoUrl;
        Country = Guard.AgainstInvalidIsoCode(country, 2, nameof(country));
        DefaultCurrency = Guard.AgainstInvalidIsoCode(defaultCurrency, 3, nameof(defaultCurrency));
        TimeZoneId = Guard.AgainstInvalidTimeZone(timeZoneId, nameof(timeZoneId));
        IsActive = true;
        DocumentQuota = documentQuota;
        SubscriptionTier = SubscriptionTier.Free;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string name, string email, string? tagline, string? address, string? logoUrl, string country, string defaultCurrency, string timeZoneId)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Tagline = tagline;
        Address = address;
        LogoUrl = logoUrl;
        Country = Guard.AgainstInvalidIsoCode(country, 2, nameof(country));
        DefaultCurrency = Guard.AgainstInvalidIsoCode(defaultCurrency, 3, nameof(defaultCurrency));
        TimeZoneId = Guard.AgainstInvalidTimeZone(timeZoneId, nameof(timeZoneId));
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

    /// <summary>
    /// Grants every new registration a one-time 7-day Full Access trial —
    /// called from RegisterCommandHandler for any workspace that didn't win the
    /// (better, 365-day) early-bird promo instead. Unlike <see cref="ActivatePaidSubscription"/>,
    /// this sets <see cref="IsFreeTrial"/> so the billing page can show "free
    /// trial" rather than "paid plan", and unlike the promo it's expected to
    /// actually lapse — see <see cref="EffectiveSubscriptionTier"/>, which is what
    /// enforces that expiry (this property alone doesn't).
    /// </summary>
    public void StartFreeTrial(DateTimeOffset registeredAtUtc)
    {
        SubscriptionTier = SubscriptionTier.Pro;
        SubscriptionEndDate = registeredAtUtc.AddDays(7);
        IsFreeTrial = true;
    }

    /// <summary>
    /// TASK-039: activates a paid tier once an admin approves a manual GCash
    /// payment submission, an upgrade request, or (once configured) a real
    /// PayMongo checkout completes. Doesn't touch IsEarlyBirdPromo: this is a
    /// standard paid upgrade, not the promo. Does clear IsFreeTrial — a customer
    /// who pays mid-trial is now a real paying customer, not a trialist.
    /// </summary>
    public void ActivatePaidSubscription(SubscriptionTier tier, DateTimeOffset expiresAtUtc)
    {
        if (tier == SubscriptionTier.Free)
        {
            throw new ArgumentOutOfRangeException(nameof(tier), tier, "Cannot activate a paid subscription for the Free tier.");
        }

        SubscriptionTier = tier;
        SubscriptionEndDate = expiresAtUtc;
        IsFreeTrial = false;
    }

    /// <summary>
    /// The tier that should actually govern plan limits and billing display right
    /// now: <see cref="SubscriptionTier"/> itself, unless it's a time-boxed paid
    /// grant (trial, early-bird promo, or a paid subscription that was never
    /// renewed) whose <see cref="SubscriptionEndDate"/> has already passed, in
    /// which case it's Free. SubscriptionTier/SubscriptionEndDate are deliberately
    /// left untouched when this happens — there's no background job reverting an
    /// expired grant, so every caller that cares about "is this workspace
    /// actually still paid" (CreateDocumentCommand's quota check,
    /// GetWorkspaceBillingQuery's display) must call this instead of reading
    /// SubscriptionTier raw.
    /// </summary>
    public SubscriptionTier EffectiveSubscriptionTier(DateTimeOffset nowUtc) =>
        SubscriptionTier != SubscriptionTier.Free && SubscriptionEndDate is { } endDate && endDate <= nowUtc
            ? SubscriptionTier.Free
            : SubscriptionTier;
}
