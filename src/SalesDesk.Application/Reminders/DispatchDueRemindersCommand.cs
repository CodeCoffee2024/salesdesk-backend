using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Reminders;

/// <summary>
/// The automated reminder engine's single entry point (TASK-025), invoked
/// periodically by the Infrastructure-layer background service. Evaluates every
/// workspace with reminders enabled against the three trigger rules, skips anything
/// already sent or suppressed by the 24-hour-per-client window, sends what's left via
/// <see cref="IEmailSender"/>, and logs each send so a later run never repeats it.
/// Returns the number of reminders actually sent.
/// </summary>
public sealed record DispatchDueRemindersCommand : IRequest<int>;

public sealed class DispatchDueRemindersCommandHandler(
    IApplicationDbContext context,
    IEmailSender emailSender,
    IPublicLinkBuilder linkBuilder,
    IDateTime dateTime)
    : IRequestHandler<DispatchDueRemindersCommand, int>
{
    public async Task<int> Handle(DispatchDueRemindersCommand request, CancellationToken cancellationToken)
    {
        var now = dateTime.UtcNow.UtcDateTime;
        var today = DateOnly.FromDateTime(now);

        var settingsByWorkspace = await context.ReminderSettingsEntries
            .Where(s => s.IsEnabled)
            .ToDictionaryAsync(s => s.WorkspaceId, cancellationToken);

        if (settingsByWorkspace.Count == 0)
        {
            return 0;
        }

        var workspaceIds = settingsByWorkspace.Keys.ToList();

        // Sent/Overdue only: Draft never went to a client, Accepted/Paid need no
        // further nudging. Status alone can't tell an invoice is overdue (nothing
        // in the codebase flips it automatically), so DetermineDueReminder recomputes
        // "overdue" from DueDate itself rather than trusting Status for that.
        var candidates = await context.Documents
            .Include(d => d.Customer)
            .Where(d => workspaceIds.Contains(d.WorkspaceId)
                && (d.Status == DocumentStatus.Sent || d.Status == DocumentStatus.Overdue))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var candidateIds = candidates.Select(d => d.Id).ToList();
        var loggedKeys = (await context.DocumentReminderLogs
                .Where(l => candidateIds.Contains(l.DocumentId))
                .Select(l => new { l.DocumentId, l.Type })
                .ToListAsync(cancellationToken))
            .Select(x => (x.DocumentId, x.Type))
            .ToHashSet();

        var suppressionCutoff = now.AddHours(-24);
        var recentlyRemindedDocumentIds = await context.DocumentReminderLogs
            .Where(l => l.SentAtUtc >= suppressionCutoff)
            .Select(l => l.DocumentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var suppressedCustomerIds = (await context.Documents
                .Where(d => recentlyRemindedDocumentIds.Contains(d.Id) && workspaceIds.Contains(d.WorkspaceId))
                .Select(d => d.CustomerId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var sentCount = 0;

        foreach (var document in candidates)
        {
            var settings = settingsByWorkspace[document.WorkspaceId];
            var reminderType = DetermineDueReminder(document, settings, today);

            if (reminderType is null || loggedKeys.Contains((document.Id, reminderType.Value)))
            {
                continue;
            }

            // Enforced per-run as well as across runs: a customer with two due
            // documents on the same day gets exactly one email today, not two.
            if (suppressedCustomerIds.Contains(document.CustomerId))
            {
                continue;
            }

            await SendReminderAsync(document, reminderType.Value, settings, now, cancellationToken);
            suppressedCustomerIds.Add(document.CustomerId);
            sentCount++;
        }

        return sentCount;
    }

    /// <summary>
    /// Which single reminder (if any) is due for this document today. Overdue
    /// checks are evaluated most-severe-first: once a document reaches the +7 final
    /// notice, that's what's returned even if the +1 first notice was never sent
    /// (e.g. it was suppressed every day it was eligible) — resending a stale "1 day
    /// late" notice once a document is a week overdue would be more confusing than
    /// useful.
    /// </summary>
    private static ReminderType? DetermineDueReminder(Document document, ReminderSettings settings, DateOnly today)
    {
        if (document.Type == DocumentType.Quote)
        {
            if (settings.QuoteFollowUpEnabled && document.Status == DocumentStatus.Sent && document.IssueDate.AddDays(3) <= today)
            {
                return ReminderType.QuoteFollowUp;
            }

            return null;
        }

        if (settings.OverdueNoticesEnabled && document.DueDate.AddDays(7) <= today)
        {
            return ReminderType.InvoiceOverdueFinalNotice;
        }

        if (settings.OverdueNoticesEnabled && document.DueDate.AddDays(1) <= today)
        {
            return ReminderType.InvoiceOverdueFirstNotice;
        }

        if (settings.InvoiceDueWarningEnabled && today <= document.DueDate && document.DueDate.AddDays(-2) <= today)
        {
            return ReminderType.InvoiceDueSoon;
        }

        return null;
    }

    private async Task SendReminderAsync(Document document, ReminderType type, ReminderSettings settings, DateTime sentAtUtc, CancellationToken cancellationToken)
    {
        var workspace = await context.Workspaces.FirstAsync(w => w.Id == document.WorkspaceId, cancellationToken);
        var documentUrl = linkBuilder.BuildDocumentUrl(document.PublicToken);
        var (subject, htmlBody) = ReminderEmailTemplates.Build(type, document, workspace, documentUrl);

        await emailSender.SendAsync(new EmailMessage(document.Customer!.Email, settings.CcEmail, subject, htmlBody, ReplyTo: workspace.Email), cancellationToken);

        // Saved immediately (not batched until the end of the run) so a mid-run
        // crash can't cause an already-sent reminder to be sent again on the next tick.
        context.DocumentReminderLogs.Add(new DocumentReminderLog(document.Id, type, sentAtUtc));
        await context.SaveChangesAsync(cancellationToken);
    }
}
