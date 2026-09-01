namespace SalesDesk.Application.Customers;

public sealed class CustomerDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Company { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    /// <summary>Optional ISO 3166-1 alpha-2 code — the default a new document's ClientCountry override is drawn from (TASK-029).</summary>
    public string? Country { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Total of this customer's paid invoices — their revenue contribution to date.</summary>
    public decimal LifetimeValue { get; set; }
}
