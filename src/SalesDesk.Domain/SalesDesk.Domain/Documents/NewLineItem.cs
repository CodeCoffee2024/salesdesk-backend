namespace SalesDesk.Domain.Documents;

/// <summary>
/// The shape of a line item to add, used by <see cref="Document.ReplaceLineItems"/>
/// to describe a full replacement set without exposing <see cref="DocumentLineItem"/>
/// construction outside the aggregate.
/// </summary>
public readonly record struct NewLineItem(string Description, decimal Quantity, decimal UnitPrice, Guid? ProductId);
