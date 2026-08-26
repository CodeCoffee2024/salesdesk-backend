namespace SalesDesk.Application.Customers;

public sealed class CustomerDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Company { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Total of this customer's paid invoices — their revenue contribution to date.</summary>
    public decimal LifetimeValue { get; set; }
}
