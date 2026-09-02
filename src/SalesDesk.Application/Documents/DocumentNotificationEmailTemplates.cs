using SalesDesk.Application.Common.Email;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Documents;

/// <summary>
/// TASK-034, Templates 1 &amp; 2 ("Quote Sent / Updated" and "Invoice Issued / Payment
/// Request"): both share one trigger (a document's status changing to Sent, see
/// UpdateDocumentStatusCommandHandler) and near-identical structure, so one builder
/// picks quote-vs-invoice wording rather than duplicating the shell twice.
/// Workspace-branded, never system-branded: this goes to the workspace's own customer.
/// </summary>
internal static class DocumentNotificationEmailTemplates
{
    public static (string Subject, string HtmlBody) BuildSentNotification(Document document, Workspace workspace, string documentUrl)
    {
        var customerName = document.Customer?.Name ?? "there";
        var isQuote = document.Type == DocumentType.Quote;

        var subject = $"{(isQuote ? "Quote" : "Invoice")} {document.DocumentNumber} from {workspace.Name}";

        var dateLine = isQuote
            ? $"<p>This quote is valid until <strong>{document.DueDate:MMM d, yyyy}</strong>.</p>"
            : $"<p>Payment is due by <strong>{document.DueDate:MMM d, yyyy}</strong>.</p>";

        var body = $"""
            <p>Hi {customerName},</p>
            <p>{workspace.Name} sent you {(isQuote ? "a quote" : "an invoice")}, <strong>{document.DocumentNumber}</strong>, totaling <strong>{document.Total:C}</strong>.</p>
            {dateLine}
            {EmailBranding.CtaButton(isQuote ? "View quote" : "View and pay invoice", documentUrl)}
            """;

        return (subject, EmailBranding.Workspace(workspace.Name, workspace.LogoUrl, workspace.Tagline, workspace.Address, workspace.Email, body));
    }
}
