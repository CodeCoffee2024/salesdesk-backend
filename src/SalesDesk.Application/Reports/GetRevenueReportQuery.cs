using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Reports;

/// <summary>
/// Backs GET /api/reports/revenue (TASK-043) — revenue bucketed over the
/// selected date range, plus the outstanding balance for that same range (kept
/// in this one query rather than a separate endpoint, since both are simple
/// aggregates over the same Documents table and should always reflect the same
/// filter as the chart).
/// </summary>
public sealed record GetRevenueReportQuery(DateOnly? From, DateOnly? To) : IRequest<RevenueReportDto>;

public sealed class GetRevenueReportQueryHandler(
    IApplicationDbContext context, IDateTime dateTime, ICurrentUserService currentUser, ICurrencyConversionService currencyConversion)
    : IRequestHandler<GetRevenueReportQuery, RevenueReportDto>
{
    // A fixed granularity would either flatten a one-week custom range to a
    // single bar or explode a 12-month range into ~365 unreadable ones — switch
    // to daily buckets under ~2 months, monthly otherwise.
    private const int DailyBucketThresholdDays = 62;

    public async Task<RevenueReportDto> Handle(GetRevenueReportQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var (from, to) = ReportDateRange.Resolve(request.From, request.To, dateTime);

        var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        var baseCurrency = workspace?.DefaultCurrency ?? "USD";

        var useDailyBuckets = to.DayNumber - from.DayNumber <= DailyBucketThresholdDays;

        // Currency varies per document (TASK-029), so Convert can't be pushed
        // into SQL — materialize the filtered rows, then bucket/convert/sum
        // in-memory, same idiom as GetDashboardSummaryQueryHandler.
        var paidRows = await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Invoice && d.Status == DocumentStatus.Paid
                && d.IssueDate >= from && d.IssueDate <= to)
            .Select(d => new { d.Total, d.Currency, d.IssueDate })
            .ToListAsync(cancellationToken);

        var amountByBucket = paidRows
            .GroupBy(r => useDailyBuckets ? r.IssueDate : new DateOnly(r.IssueDate.Year, r.IssueDate.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(r => currencyConversion.Convert(r.Total, r.Currency, baseCurrency)));

        var points = new List<RevenuePointDto>();
        if (useDailyBuckets)
        {
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                points.Add(new RevenuePointDto { Label = day.ToString("MMM d"), Amount = amountByBucket.GetValueOrDefault(day) });
            }
        }
        else
        {
            for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            {
                points.Add(new RevenuePointDto { Label = month.ToString("MMM yyyy"), Amount = amountByBucket.GetValueOrDefault(month) });
            }
        }

        var outstanding = (await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Invoice
                && (d.Status == DocumentStatus.Sent || d.Status == DocumentStatus.Overdue)
                && d.IssueDate >= from && d.IssueDate <= to)
            .Select(d => new { d.Total, d.Currency })
            .ToListAsync(cancellationToken))
            .Sum(d => currencyConversion.Convert(d.Total, d.Currency, baseCurrency));

        return new RevenueReportDto { Points = points, Outstanding = outstanding, BaseCurrency = baseCurrency };
    }
}
