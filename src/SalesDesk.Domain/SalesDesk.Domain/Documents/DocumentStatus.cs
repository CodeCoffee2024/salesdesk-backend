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
    Paid
}
