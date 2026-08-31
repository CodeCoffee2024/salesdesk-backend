namespace SalesDesk.Application.Reminders;

public sealed class ReminderSettingsDto
{
    public bool IsEnabled { get; init; }

    public bool QuoteFollowUpEnabled { get; init; }

    public bool InvoiceDueWarningEnabled { get; init; }

    public bool OverdueNoticesEnabled { get; init; }

    public string? CcEmail { get; init; }
}
