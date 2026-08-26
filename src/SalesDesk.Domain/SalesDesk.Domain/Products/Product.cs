using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Products;

/// <summary>
/// A billable item in the workspace's catalog, reused across quotes and invoices
/// to keep pricing and descriptions consistent.
/// </summary>
public sealed class Product : Entity
{
    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public ProductUnit Unit { get; private set; }

    public string? Category { get; private set; }

    private Product()
    {
        Name = string.Empty;
    }

    public Product(Guid workspaceId, string name, decimal price, ProductUnit unit, string? description = null, string? category = null)
    {
        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Price = Guard.AgainstNegative(price, nameof(price));
        Unit = unit;
        Description = description;
        Category = category;
    }

    public void UpdateDetails(string name, decimal price, ProductUnit unit, string? description, string? category)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Price = Guard.AgainstNegative(price, nameof(price));
        Unit = unit;
        Description = description;
        Category = category;
    }
}
