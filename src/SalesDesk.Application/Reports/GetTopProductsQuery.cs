using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Reports;

/// <summary>
/// Backs GET /api/reports/top-products (TASK-043) — the 5 catalog products/
/// services billed the most (non-Draft invoices) by total line-value within the
/// selected range, converted into the workspace's base currency. Ranked by
/// money, not quantity, since price varies wildly across products (hours vs.
/// flat-fee packages vs. pieces) and every other figure on this page answers
/// "how much," not "how many." Free-text line items (ProductId is null, never
/// linked to the catalog) are excluded rather than folded into an "Other"
/// bucket — they aren't a product.
/// </summary>
public sealed record GetTopProductsQuery(DateOnly? From, DateOnly? To) : IRequest<List<TopProductDto>>;

public sealed class GetTopProductsQueryHandler(
    IApplicationDbContext context, IDateTime dateTime, ICurrentUserService currentUser, ICurrencyConversionService currencyConversion)
    : IRequestHandler<GetTopProductsQuery, List<TopProductDto>>
{
    private const int TopCount = 5;

    public async Task<List<TopProductDto>> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var (from, to) = ReportDateRange.Resolve(request.From, request.To, dateTime);

        var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        var baseCurrency = workspace?.DefaultCurrency ?? "USD";

        var rows = await context.DocumentLineItems
            .Where(li => li.ProductId != null
                && li.Document!.WorkspaceId == workspaceId
                && li.Document.Type == DocumentType.Invoice
                && li.Document.Status != DocumentStatus.Draft
                && li.Document.IssueDate >= from && li.Document.IssueDate <= to)
            .Select(li => new { ProductId = li.ProductId!.Value, Name = li.Product!.Name, li.Quantity, li.LineTotal, Currency = li.Document!.Currency })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ProductId)
            .Select(g => new TopProductDto
            {
                ProductId = g.Key,
                Name = g.First().Name,
                Quantity = g.Sum(r => r.Quantity),
                Amount = g.Sum(r => currencyConversion.Convert(r.LineTotal, r.Currency, baseCurrency))
            })
            .OrderByDescending(p => p.Amount)
            .Take(TopCount)
            .ToList();
    }
}
