using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Reports;

/// <summary>
/// TASK-043: resolves the effective date range for every Reports query — one
/// seam shared by all three handlers instead of duplicating the same default
/// logic three times. Defaults to the current calendar month to date (matching
/// the frontend's "This month" preset exactly, so a first page load and
/// clicking "This month" always agree), and tolerates a caller passing the
/// bounds in the wrong order rather than erroring.
/// </summary>
public static class ReportDateRange
{
    public static (DateOnly From, DateOnly To) Resolve(DateOnly? from, DateOnly? to, IDateTime dateTime)
    {
        var today = DateOnly.FromDateTime(dateTime.UtcNow.Date);
        var resolvedTo = to ?? today;
        var resolvedFrom = from ?? new DateOnly(resolvedTo.Year, resolvedTo.Month, 1);

        return resolvedFrom <= resolvedTo ? (resolvedFrom, resolvedTo) : (resolvedTo, resolvedFrom);
    }
}
