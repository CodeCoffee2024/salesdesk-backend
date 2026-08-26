using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Dashboard;

public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public sealed class GetDashboardSummaryQueryHandler(IApplicationDbContext context, IDateTime dateTime, ICurrentUserService currentUser)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var today = DateOnly.FromDateTime(dateTime.UtcNow.Date);
        var currentYear = today.Year;
        var currentQuarterStartMonth = ((today.Month - 1) / 3 * 3) + 1;
        var currentQuarterStart = new DateOnly(today.Year, currentQuarterStartMonth, 1);

        var revenueThisYear = await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Invoice && d.Status == DocumentStatus.Paid && d.IssueDate.Year == currentYear)
            .SumAsync(d => (decimal?)d.Total, cancellationToken) ?? 0m;

        var outstanding = await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Invoice && (d.Status == DocumentStatus.Sent || d.Status == DocumentStatus.Overdue))
            .SumAsync(d => (decimal?)d.Total, cancellationToken) ?? 0m;

        var quotePipeline = await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Quote && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.Sent))
            .SumAsync(d => (decimal?)d.Total, cancellationToken) ?? 0m;

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
            ActiveCustomers = activeCustomers
        };
    }
}
