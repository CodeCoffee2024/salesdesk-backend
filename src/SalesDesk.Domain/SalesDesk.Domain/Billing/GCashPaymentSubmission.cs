using System.Text.RegularExpressions;
using SalesDesk.Domain.Common;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Domain.Billing;

/// <summary>
/// TASK-039: a workspace's claim to have paid for a paid tier via a personal GCash
/// transfer to the platform's own GCash account — PayMongo/Stripe/PayPal aren't
/// configured yet (see IPaymentGatewayService), so for Philippine subscribers this
/// manual claim-and-verify flow is the actual, working upgrade path rather than a
/// stub. A platform admin verifies the reference number against their own GCash
/// app and approves it from a one-click emailed link; nothing here charges anyone
/// or talks to a payment processor — it only records what the submitter claims and
/// gates the real tier upgrade behind a human's approval.
/// </summary>
public sealed class GCashPaymentSubmission : Entity
{
    private static readonly Regex ReferenceNumberPattern = new("^\\d{13}$", RegexOptions.Compiled);

    public Guid WorkspaceId { get; private set; }

    /// <summary>Always Pro or Studio — there's nothing to pay for on Free.</summary>
    public SubscriptionTier Tier { get; private set; }

    /// <summary>"Monthly" or "Annual" — determines the subscription length Approve grants.</summary>
    public string BillingCycle { get; private set; }

    /// <summary>PHP amount the submitter was quoted at submission time (TASK-038's PH pricing catalog) — kept even though it isn't charged automatically, so the admin approval email shows the exact amount to reconcile against the GCash app.</summary>
    public decimal AmountPhp { get; private set; }

    public string GCashReferenceNumber { get; private set; }

    public string SenderName { get; private set; }

    public string SenderMobileNumber { get; private set; }

    /// <summary>Optional proof-of-payment screenshot, stored as a PNG/JPEG data URL — same inline-image approach as DocumentSignature.SignatureImageDataUrl, since this platform has no separate file/object storage.</summary>
    public string? ScreenshotDataUrl { get; private set; }

    /// <summary>SHA-256 hash of the raw token emailed to the admin (SecureTokens) — only the hash is persisted, so a database leak alone can't be replayed as a working approval link.</summary>
    public string ApprovalTokenHash { get; private set; }

    public bool IsApproved { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }

    private GCashPaymentSubmission()
    {
        BillingCycle = string.Empty;
        GCashReferenceNumber = string.Empty;
        SenderName = string.Empty;
        SenderMobileNumber = string.Empty;
        ApprovalTokenHash = string.Empty;
    }

    public GCashPaymentSubmission(
        Guid workspaceId,
        SubscriptionTier tier,
        string billingCycle,
        decimal amountPhp,
        string gcashReferenceNumber,
        string senderName,
        string senderMobileNumber,
        string? screenshotDataUrl,
        string approvalTokenHash,
        DateTimeOffset submittedAtUtc)
    {
        if (tier == SubscriptionTier.Free)
        {
            throw new ArgumentOutOfRangeException(nameof(tier), tier, "A GCash payment submission must be for a paid tier (Pro or Studio).");
        }

        if (billingCycle is not ("Monthly" or "Annual"))
        {
            throw new ArgumentOutOfRangeException(nameof(billingCycle), billingCycle, "Billing cycle must be 'Monthly' or 'Annual'.");
        }

        if (!ReferenceNumberPattern.IsMatch(gcashReferenceNumber ?? string.Empty))
        {
            throw new ArgumentException("GCash reference number must be exactly 13 digits.", nameof(gcashReferenceNumber));
        }

        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        Tier = tier;
        BillingCycle = billingCycle;
        AmountPhp = Guard.AgainstNegativeOrZero(amountPhp, nameof(amountPhp));
        GCashReferenceNumber = gcashReferenceNumber!;
        SenderName = Guard.AgainstNullOrWhiteSpace(senderName, nameof(senderName));
        SenderMobileNumber = Guard.AgainstNullOrWhiteSpace(senderMobileNumber, nameof(senderMobileNumber));
        ScreenshotDataUrl = screenshotDataUrl;
        ApprovalTokenHash = Guard.AgainstNullOrWhiteSpace(approvalTokenHash, nameof(approvalTokenHash));
        SubmittedAtUtc = submittedAtUtc;
    }

    /// <summary>Idempotent on purpose — the emailed link can be opened more than once (a mail client prefetching links, an admin double-clicking); a second visit is a no-op, not an error.</summary>
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
