using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Dashboard;

public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public sealed class GetDashboardSummaryQueryHandler(
    IApplicationDbContext context, IDateTime dateTime, ICurrentUserService currentUser, ICurrencyConversionService currencyConversion)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var today = DateOnly.FromDateTime(dateTime.UtcNow.Date);
        var currentYear = today.Year;
        var currentQuarterStartMonth = ((today.Month - 1) / 3 * 3) + 1;
        var currentQuarterStart = new DateOnly(today.Year, currentQuarterStartMonth, 1);

        // Not every caller seeds a Workspace row (see CreateDocumentCommandHandler
        // for the same fallback rationale) — default the base currency to USD rather
        // than throwing.
        var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        var baseCurrency = workspace?.DefaultCurrency ?? "USD";

        // Documents can be priced in different currencies (TASK-029), so the sum
        // can't be pushed down into SQL — pull (Total, Currency) pairs back and
        // convert each into the workspace's base currency before aggregating.
        var revenueThisYear = (await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Invoice && d.Status == DocumentStatus.Paid && d.IssueDate.Year == currentYear)
            .Select(d => new { d.Total, d.Currency })
            .ToListAsync(cancellationToken))
            .Sum(d => currencyConversion.Convert(d.Total, d.Currency, baseCurrency));

        var outstanding = (await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Invoice && (d.Status == DocumentStatus.Sent || d.Status == DocumentStatus.Overdue))
            .Select(d => new { d.Total, d.Currency })
            .ToListAsync(cancellationToken))
            .Sum(d => currencyConversion.Convert(d.Total, d.Currency, baseCurrency));

        var quotePipeline = (await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Quote && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.Sent))
            .Select(d => new { d.Total, d.Currency })
            .ToListAsync(cancellationToken))
            .Sum(d => currencyConversion.Convert(d.Total, d.Currency, baseCurrency));

        var activeCustomers = await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.IssueDate >= currentQuarterStart)
            .Select(d => d.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            RevenueThisYear = revenueThisYear,
            Outstanding = outstanding,
            QuotePipeline = quotePipeline,
            ActiveCustomers = activeCustomers,
            BaseCurrency = baseCurrency
        };
    }
}
