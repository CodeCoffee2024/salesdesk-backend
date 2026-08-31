using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Reminders;

/// <summary>Backs PUT /api/settings/reminders (TASK-025). Upserts, since a workspace has no row until it first saves settings.</summary>
public sealed record SaveReminderSettingsCommand(
    bool IsEnabled,
    bool QuoteFollowUpEnabled,
    bool InvoiceDueWarningEnabled,
    bool OverdueNoticesEnabled,
    string? CcEmail) : IRequest<ReminderSettingsDto>;

public sealed class SaveReminderSettingsCommandValidator : AbstractValidator<SaveReminderSettingsCommand>
{
    public SaveReminderSettingsCommandValidator()
    {
        RuleFor(c => c.CcEmail).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.CcEmail));
    }
}

public sealed class SaveReminderSettingsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<SaveReminderSettingsCommand, ReminderSettingsDto>
{
    public async Task<ReminderSettingsDto> Handle(SaveReminderSettingsCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var settings = await context.ReminderSettingsEntries
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId, cancellationToken);

        if (settings is null)
        {
            settings = new ReminderSettings(
                workspaceId,
                request.IsEnabled,
                request.QuoteFollowUpEnabled,
                request.InvoiceDueWarningEnabled,
                request.OverdueNoticesEnabled,
                request.CcEmail);
            context.ReminderSettingsEntries.Add(settings);
        }
        else
        {
            settings.Update(
                request.IsEnabled,
                request.QuoteFollowUpEnabled,
                request.InvoiceDueWarningEnabled,
                request.OverdueNoticesEnabled,
                request.CcEmail);
        }

        await context.SaveChangesAsync(cancellationToken);

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
