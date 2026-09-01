namespace SalesDesk.Application.Workspaces;

/// <summary>TASK-031: backs GET /api/workspace/billing — the /settings/billing page's subscription-tier badge and early-bird promo details.</summary>
public sealed class WorkspaceBillingDto
{
    public string SubscriptionTier { get; init; } = "Free";

    /// <summary>Null for a Free workspace, or a Premium grant that isn't time-boxed (not possible yet — the early-bird promo is the only path to Premium so far, and always sets this).</summary>
    public DateTimeOffset? SubscriptionEndDate { get; init; }

    /// <summary>True if this workspace was one of the first 100 eligible registrations.</summary>
    public bool IsEarlyBirdPromo { get; init; }
}
