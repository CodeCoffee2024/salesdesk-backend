using SalesDesk.Domain.Common;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Domain.Billing;

/// <summary>
/// A workspace's request to be manually upgraded to a paid tier when no
/// configured payment method fits — no card/PayPal gateway exists yet
/// (<see cref="Common.Interfaces.IPaymentGatewayService"/> is a stub) and GCash
/// (<see cref="GCashPaymentSubmission"/>) only applies to PH workspaces. Unlike a
/// GCash submission, this carries no proof of payment — it's simply "we'd like to
/// subscribe, please arrange billing with us" — so approval is a manual business
/// decision by the platform admin, not verification of a claimed transaction.
/// </summary>
public sealed class SubscriptionUpgradeRequest : Entity
{
    public Guid WorkspaceId { get; private set; }

    /// <summary>Always Pro or Studio — there's nothing to request on Free.</summary>
    public SubscriptionTier Tier { get; private set; }

    /// <summary>"Monthly" or "Annual" — determines the subscription length Approve grants.</summary>
    public string BillingCycle { get; private set; }

    /// <summary>Optional context from the requester (e.g. "no GCash account, please invoice us directly").</summary>
    public string? Note { get; private set; }

    /// <summary>SHA-256 hash of the raw token emailed to the admin (SecureTokens) — only the hash is persisted, so a database leak alone can't be replayed as a working approval link.</summary>
    public string ApprovalTokenHash { get; private set; }

    public bool IsApproved { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public DateTimeOffset RequestedAtUtc { get; private set; }

    private SubscriptionUpgradeRequest()
    {
        BillingCycle = string.Empty;
        ApprovalTokenHash = string.Empty;
    }

    public SubscriptionUpgradeRequest(
        Guid workspaceId,
        SubscriptionTier tier,
        string billingCycle,
        string? note,
        string approvalTokenHash,
        DateTimeOffset requestedAtUtc)
    {
        if (tier == SubscriptionTier.Free)
        {
            throw new ArgumentOutOfRangeException(nameof(tier), tier, "An upgrade request must be for a paid tier (Pro or Studio).");
        }

        if (billingCycle is not ("Monthly" or "Annual"))
        {
            throw new ArgumentOutOfRangeException(nameof(billingCycle), billingCycle, "Billing cycle must be 'Monthly' or 'Annual'.");
        }

        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        Tier = tier;
        BillingCycle = billingCycle;
        Note = note;
        ApprovalTokenHash = Guard.AgainstNullOrWhiteSpace(approvalTokenHash, nameof(approvalTokenHash));
        RequestedAtUtc = requestedAtUtc;
    }

    /// <summary>Idempotent on purpose — the emailed link can be opened more than once; a second visit is a no-op, not an error.</summary>
    public void Approve(DateTimeOffset approvedAtUtc)
    {
        if (IsApproved)
        {
            return;
        }

        IsApproved = true;
        ApprovedAtUtc = approvedAtUtc;
    }

    /// <summary>Grant length Approve translates BillingCycle into, on the workspace being upgraded.</summary>
    public TimeSpan SubscriptionLength => BillingCycle == "Annual" ? TimeSpan.FromDays(365) : TimeSpan.FromDays(30);
}
