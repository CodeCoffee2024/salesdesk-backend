namespace SalesDesk.Domain.Templates;

/// <summary>
/// Which kind(s) of document a template may be applied to.
/// </summary>
public enum TemplateTargetType
{
    QuotesAndInvoices,
    QuotesOnly,
    InvoicesOnly
}
