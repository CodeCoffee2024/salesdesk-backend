using SalesDesk.Domain.Common;
using SalesDesk.Domain.Products;

namespace SalesDesk.Domain.Documents;

/// <summary>
/// One priced line on a <see cref="Document"/>. Line items only exist as part of a
/// document, so this type is constructed exclusively through
/// <see cref="Document.AddLineItem"/> — never directly.
/// </summary>
public sealed class DocumentLineItem : Entity
{
    public Guid DocumentId { get; private set; }

    public Document? Document { get; private set; }

    public Guid? ProductId { get; private set; }

    public Product? Product { get; private set; }

    public string Description { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal { get; private set; }

    private DocumentLineItem()
    {
        Description = string.Empty;
    }

    internal DocumentLineItem(Guid documentId, string description, decimal quantity, decimal unitPrice, Guid? productId)
    {
        DocumentId = Guard.AgainstEmpty(documentId, nameof(documentId));
        Description = Guard.AgainstNullOrWhiteSpace(description, nameof(description));
        Quantity = Guard.AgainstNegativeOrZero(quantity, nameof(quantity));
        UnitPrice = Guard.AgainstNegative(unitPrice, nameof(unitPrice));
        ProductId = productId;
        LineTotal = Quantity * UnitPrice;
    }

    internal void UpdateDetails(string description, decimal quantity, decimal unitPrice, Guid? productId)
    {
        Description = Guard.AgainstNullOrWhiteSpace(description, nameof(description));
        Quantity = Guard.AgainstNegativeOrZero(quantity, nameof(quantity));
        UnitPrice = Guard.AgainstNegative(unitPrice, nameof(unitPrice));
        ProductId = productId;
        LineTotal = Quantity * UnitPrice;
    }
}
