namespace SalesDesk.Domain.Documents;

/// <summary>
/// Which automated reminder rule (TASK-025) fired for a document. Each value maps to
/// exactly one trigger rule and is logged at most once per document via
/// <see cref="DocumentReminderLog"/>, which is what keeps the dispatch engine
/// idempotent across repeated runs.
/// </summary>
public enum ReminderType
{
    /// <summary>Quote sent at least 3 days ago and still awaiting a client response.</summary>
    QuoteFollowUp,

    /// <summary>Invoice due within 2 days and not yet paid.</summary>
    InvoiceDueSoon,

    /// <summary>Invoice at least 1 day past its due date.</summary>
    InvoiceOverdueFirstNotice,

    /// <summary>Invoice at least 7 days past its due date.</summary>
    InvoiceOverdueFinalNotice
}
