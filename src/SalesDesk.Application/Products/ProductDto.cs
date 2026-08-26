using SalesDesk.Domain.Products;

namespace SalesDesk.Application.Products;

public sealed class ProductDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public decimal Price { get; init; }

    public ProductUnit Unit { get; init; }

    public string? Category { get; init; }
}
