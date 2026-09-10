using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Reports;

/// <summary>
/// Backs GET /api/reports/top-customers (TASK-043) — the 5 customers billed the
/// most (non-Draft invoices, so Sent/Overdue count even before payment) within
/// the selected range, converted into the workspace's base currency.
/// </summary>
public sealed record GetTopCustomersQuery(DateOnly? From, DateOnly? To) : IRequest<List<TopCustomerDto>>;

public sealed class GetTopCustomersQueryHandler(
    IApplicationDbContext context, IDateTime dateTime, ICurrentUserService currentUser, ICurrencyConversionService currencyConversion)
    : IRequestHandler<GetTopCustomersQuery, List<TopCustomerDto>>
{
    private const int TopCount = 5;

    public async Task<List<TopCustomerDto>> Handle(GetTopCustomersQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var (from, to) = ReportDateRange.Resolve(request.From, request.To, dateTime);

        var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        var baseCurrency = workspace?.DefaultCurrency ?? "USD";

        var rows = await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Invoice && d.Status != DocumentStatus.Draft
                && d.IssueDate >= from && d.IssueDate <= to)
            .Select(d => new { d.CustomerId, Name = d.Customer!.Name, Company = d.Customer.Company, d.Total, d.Currency })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.CustomerId)
            .Select(g => new TopCustomerDto
            {
                CustomerId = g.Key,
                Name = g.First().Name,
                Company = g.First().Company,
                BilledTotal = g.Sum(r => currencyConversion.Convert(r.Total, r.Currency, baseCurrency))
            })
            .OrderByDescending(c => c.BilledTotal)
            .Take(TopCount)
            .ToList();
    }
}
