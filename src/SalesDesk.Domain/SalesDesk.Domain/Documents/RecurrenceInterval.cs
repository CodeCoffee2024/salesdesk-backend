namespace SalesDesk.Domain.Documents;

/// <summary>How often a <see cref="RecurringSchedule"/> generates a new document.</summary>
public enum RecurrenceInterval
{
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

public static class RecurrenceIntervalExtensions
{
    /// <summary>
    /// Advances a schedule date by one period. Month-based intervals use
    /// <see cref="DateOnly.AddMonths"/>, which already clamps a day that doesn't
    /// exist in the target month (e.g. Jan 31 + 1 month lands on Feb 28/29) rather
    /// than throwing or overflowing into the next month.
    /// </summary>
    public static DateOnly AddTo(this RecurrenceInterval interval, DateOnly date) => interval switch
    {
        RecurrenceInterval.Weekly => date.AddDays(7),
        RecurrenceInterval.Monthly => date.AddMonths(1),
        RecurrenceInterval.Quarterly => date.AddMonths(3),
        RecurrenceInterval.Yearly => date.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, null)
    };
}
