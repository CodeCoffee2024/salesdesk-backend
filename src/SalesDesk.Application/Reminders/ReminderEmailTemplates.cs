using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Reminders;

/// <summary>Builds the subject/body pair for each automated reminder rule (TASK-025). Kept separate from the dispatch handler so the wording can change without touching the trigger/suppression logic.</summary>
internal static class ReminderEmailTemplates
{
    public static (string Subject, string HtmlBody) Build(ReminderType type, Document document, string documentUrl)
    {
        var customerName = document.Customer?.Name ?? "there";

        return type switch
        {
            ReminderType.QuoteFollowUp => (
                $"Following up on {document.DocumentNumber}",
                $"""
                <p>Hi {customerName},</p>
                <p>Just checking in on quote <strong>{document.DocumentNumber}</strong> ({document.Total:C}) — let us know if you have any questions.</p>
                <p><a href="{documentUrl}">View quote</a></p>
                """),

            ReminderType.InvoiceDueSoon => (
                $"{document.DocumentNumber} is due {document.DueDate:MMM d}",
                $"""
                <p>Hi {customerName},</p>
                <p>A friendly reminder that invoice <strong>{document.DocumentNumber}</strong> ({document.Total:C}) is due on {document.DueDate:MMM d, yyyy}.</p>
                <p><a href="{documentUrl}">View invoice</a></p>
                """),

            ReminderType.InvoiceOverdueFirstNotice => (
                $"{document.DocumentNumber} is now overdue",
                $"""
                <p>Hi {customerName},</p>
                <p>Invoice <strong>{document.DocumentNumber}</strong> ({document.Total:C}) was due on {document.DueDate:MMM d, yyyy} and is now overdue.</p>
                <p><a href="{documentUrl}">View invoice</a></p>
                """),

            ReminderType.InvoiceOverdueFinalNotice => (
                $"Final notice: {document.DocumentNumber} is significantly overdue",
                $"""
                <p>Hi {customerName},</p>
                <p>This is a final notice — invoice <strong>{document.DocumentNumber}</strong> ({document.Total:C}) was due on {document.DueDate:MMM d, yyyy} and remains unpaid.</p>
                <p><a href="{documentUrl}">View invoice</a></p>
                """),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown reminder type.")
        };
    }
}
