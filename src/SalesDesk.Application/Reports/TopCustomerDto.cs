namespace SalesDesk.Application.Reports;

public sealed class TopCustomerDto
{
    public Guid CustomerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Company { get; init; } = string.Empty;

    /// <summary>Sum of Total across non-Draft invoices in range, converted into the workspace's base currency.</summary>
    public decimal BilledTotal { get; init; }
}
