using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents;

/// <summary>
/// Sends the "you've been sent a quote/invoice" notification (TASK-034 Templates
/// 1/2) — shared by every path that can transition a document to Sent (create +
/// dispatch, edit + dispatch/re-send a revision, and the standalone "Mark as
/// Sent" lifecycle action), so the workspace lookup and email construction live
/// in one place instead of three near-identical copies.
/// </summary>
internal static class DocumentDispatchNotifier
{
    public static async Task NotifyAsync(
        IApplicationDbContext context,
        IEmailSender emailSender,
        IPublicLinkBuilder linkBuilder,
        Document document,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        // Not every caller (e.g. some tests) seeds a Workspace row for the current
        // workspace id, and a document loaded without its Customer navigation has
        // nowhere to send to — either way, no email rather than throwing.
        if (document.Customer is null)
        {
            return;
        }

        var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        if (workspace is null)
        {
            return;
        }

        var documentUrl = linkBuilder.BuildDocumentUrl(document.PublicToken);
        var (subject, htmlBody) = DocumentNotificationEmailTemplates.BuildSentNotification(document, workspace, documentUrl);

        await emailSender.SendAsync(new EmailMessage(document.Customer.Email, Cc: null, subject, htmlBody, ReplyTo: workspace.Email), cancellationToken);
    }
}
