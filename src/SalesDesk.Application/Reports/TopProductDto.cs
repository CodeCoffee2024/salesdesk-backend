namespace SalesDesk.Application.Reports;

public sealed class TopProductDto
{
    public Guid ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Raw summed quantity across matching line items — shown for context; ranking is by Amount, not this.</summary>
    public decimal Quantity { get; init; }

    /// <summary>Sum of line-item totals across non-Draft invoices in range, converted into the workspace's base currency.</summary>
    public decimal Amount { get; init; }
}
