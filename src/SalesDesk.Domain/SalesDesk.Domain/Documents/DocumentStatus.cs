namespace SalesDesk.Domain.Documents;

/// <summary>
/// Lifecycle state of a quote or invoice.
/// </summary>
public enum DocumentStatus
{
    Draft,
    Sent,
    Overdue,
    Accepted,
    Paid,

    /// <summary>A client asked for changes from the public document view (TASK-027) instead of accepting/signing it.</summary>
    RevisionRequested
}
