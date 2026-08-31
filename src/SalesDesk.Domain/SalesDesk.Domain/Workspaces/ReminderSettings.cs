using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Workspaces;

/// <summary>
/// A workspace's opt-in controls for the automated reminder engine (TASK-025): a
/// master on/off switch, a per-rule toggle for each of the three trigger rules, and
/// an optional address to CC on every reminder sent (so the business owner can be
/// copied on client-facing notices in addition to whatever "from" identity the email
/// goes out under). One row per workspace, created lazily the first time settings
/// are saved — a workspace with no row yet is treated as "reminders disabled".
/// </summary>
public sealed class ReminderSettings : Entity
{
    public Guid WorkspaceId { get; private set; }

    public bool IsEnabled { get; private set; }

    public bool QuoteFollowUpEnabled { get; private set; }

    public bool InvoiceDueWarningEnabled { get; private set; }

    public bool OverdueNoticesEnabled { get; private set; }

    /// <summary>Extra address CC'd on every reminder sent, alongside the customer's own email — e.g. a personal inbox the workspace owner wants copied on client-facing notices. Null means no CC.</summary>
    public string? CcEmail { get; private set; }

    private ReminderSettings()
    {
    }

    public ReminderSettings(Guid workspaceId, bool isEnabled, bool quoteFollowUpEnabled, bool invoiceDueWarningEnabled, bool overdueNoticesEnabled, string? ccEmail)
    {
        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        IsEnabled = isEnabled;
        QuoteFollowUpEnabled = quoteFollowUpEnabled;
        InvoiceDueWarningEnabled = invoiceDueWarningEnabled;
        OverdueNoticesEnabled = overdueNoticesEnabled;
        CcEmail = string.IsNullOrWhiteSpace(ccEmail) ? null : ccEmail;
    }

    public void Update(bool isEnabled, bool quoteFollowUpEnabled, bool invoiceDueWarningEnabled, bool overdueNoticesEnabled, string? ccEmail)
    {
        IsEnabled = isEnabled;
        QuoteFollowUpEnabled = quoteFollowUpEnabled;
        InvoiceDueWarningEnabled = invoiceDueWarningEnabled;
        OverdueNoticesEnabled = overdueNoticesEnabled;
        CcEmail = string.IsNullOrWhiteSpace(ccEmail) ? null : ccEmail;
    }
}
