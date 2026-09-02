using SalesDesk.Application.Common.Email;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Reminders;

/// <summary>Builds the subject/body pair for each automated reminder rule (TASK-025), wrapped in the sending workspace's own branding (TASK-034): these go to the workspace's own customer, never the platform's. Kept separate from the dispatch handler so the wording can change without touching the trigger/suppression logic.</summary>
internal static class ReminderEmailTemplates
{
    public static (string Subject, string HtmlBody) Build(ReminderType type, Document document, Workspace workspace, string documentUrl)
    {
        var customerName = document.Customer?.Name ?? "there";

        var (subject, bodyFragment) = type switch
        {
            ReminderType.QuoteFollowUp => (
                $"Following up on {document.DocumentNumber}",
                $"""
                <p>Hi {customerName},</p>
                <p>Just checking in on quote <strong>{document.DocumentNumber}</strong> ({document.Total:C}). Let us know if you have any questions.</p>
                {EmailBranding.CtaButton("View quote", documentUrl)}
                """),

            ReminderType.InvoiceDueSoon => (
                $"{document.DocumentNumber} is due {document.DueDate:MMM d}",
                $"""
                <p>Hi {customerName},</p>
                <p>A friendly reminder that invoice <strong>{document.DocumentNumber}</strong> ({document.Total:C}) is due on {document.DueDate:MMM d, yyyy}.</p>
                {EmailBranding.CtaButton("View invoice", documentUrl)}
                """),

            ReminderType.InvoiceOverdueFirstNotice => (
                $"{document.DocumentNumber} is now overdue",
                $"""
                <p>Hi {customerName},</p>
                <p>Invoice <strong>{document.DocumentNumber}</strong> ({document.Total:C}) was due on {document.DueDate:MMM d, yyyy} and is now overdue.</p>
                {EmailBranding.CtaButton("View invoice", documentUrl)}
                """),

            ReminderType.InvoiceOverdueFinalNotice => (
                $"Final notice: {document.DocumentNumber} is significantly overdue",
                $"""
                <p>Hi {customerName},</p>
                <p>This is a final notice: invoice <strong>{document.DocumentNumber}</strong> ({document.Total:C}) was due on {document.DueDate:MMM d, yyyy} and remains unpaid.</p>
                {EmailBranding.CtaButton("View invoice", documentUrl)}
                """),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown reminder type.")
        };

        return (subject, EmailBranding.Workspace(workspace.Name, workspace.LogoUrl, workspace.Tagline, workspace.Address, workspace.Email, bodyFragment));
    }
}
