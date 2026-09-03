namespace SalesDesk.Application.Workspaces;

/// <summary>TASK-031: backs GET /api/workspace/billing — the /settings/billing page's subscription-tier badge and early-bird promo details.</summary>
public sealed class WorkspaceBillingDto
{
    public string SubscriptionTier { get; init; } = "Free";

    /// <summary>Null for a Free workspace, or a Premium grant that isn't time-boxed (not possible yet — the early-bird promo is the only path to Premium so far, and always sets this).</summary>
    public DateTimeOffset? SubscriptionEndDate { get; init; }

    /// <summary>True if this workspace was one of the first 100 eligible registrations.</summary>
    public bool IsEarlyBirdPromo { get; init; }

    /// <summary>TASK-038: this tier's monthly document cap, or null when unlimited (Pro/Studio).</summary>
    public int? MonthlyDocumentLimit { get; init; }

    /// <summary>TASK-038: documents issued so far this calendar month, for the usage bar next to MonthlyDocumentLimit on /settings/billing.</summary>
    public int DocumentsIssuedThisMonth { get; init; }

    /// <summary>TASK-039: set while a GCash payment claim is awaiting admin verification — drives the "GCash Payment Received..." banner. Null once approved (or if nothing's ever been submitted).</summary>
    public PendingGCashSubmissionDto? PendingGCashSubmission { get; init; }

    /// <summary>Set while a no-payment-method-available upgrade request is awaiting manual admin approval. Null once approved (or if nothing's ever been requested).</summary>
    public PendingUpgradeRequestDto? PendingUpgradeRequest { get; init; }
}

public sealed class PendingGCashSubmissionDto
{
    public string GCashReferenceNumber { get; init; } = string.Empty;

    public string Tier { get; init; } = string.Empty;

    public DateTimeOffset SubmittedAtUtc { get; init; }
}

public sealed class PendingUpgradeRequestDto
{
    public string Tier { get; init; } = string.Empty;

    public string BillingCycle { get; init; } = string.Empty;

    public DateTimeOffset RequestedAtUtc { get; init; }
}
