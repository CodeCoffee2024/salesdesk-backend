using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Reminders;

/// <summary>Backs GET /api/settings/reminders — the current workspace's reminder-engine configuration (TASK-025).</summary>
public sealed record GetReminderSettingsQuery : IRequest<ReminderSettingsDto>;

public sealed class GetReminderSettingsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<GetReminderSettingsQuery, ReminderSettingsDto>
{
    public async Task<ReminderSettingsDto> Handle(GetReminderSettingsQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var settings = await context.ReminderSettingsEntries
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId, cancellationToken);

        if (settings is null)
        {
            // No row yet for this workspace: the engine is off overall, but each
            // individual rule defaults to "on" so turning the master switch on for
            // the first time enables the whole feature rather than three
            // individually-disabled rules the admin then has to discover and flip.
            return new ReminderSettingsDto
            {
                IsEnabled = false,
                QuoteFollowUpEnabled = true,
                InvoiceDueWarningEnabled = true,
                OverdueNoticesEnabled = true,
                CcEmail = null
            };
        }

        return new ReminderSettingsDto
        {
            IsEnabled = settings.IsEnabled,
            QuoteFollowUpEnabled = settings.QuoteFollowUpEnabled,
            InvoiceDueWarningEnabled = settings.InvoiceDueWarningEnabled,
            OverdueNoticesEnabled = settings.OverdueNoticesEnabled,
            CcEmail = settings.CcEmail
        };
    }
}
