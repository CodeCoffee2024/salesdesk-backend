using SalesDesk.Domain.Common;
using SalesDesk.Domain.Products;

namespace SalesDesk.Domain.Documents;

/// <summary>
/// One line item template on a <see cref="RecurringSchedule"/> — copied onto every
/// document the schedule generates. Only exists as part of a schedule, constructed
/// exclusively through <see cref="RecurringSchedule.AddLineItem"/>.
/// </summary>
public sealed class RecurringScheduleLineItem : Entity
{
    public Guid RecurringScheduleId { get; private set; }

    public RecurringSchedule? RecurringSchedule { get; private set; }

    public Guid? ProductId { get; private set; }

    public Product? Product { get; private set; }

    public string Description { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    private RecurringScheduleLineItem()
    {
        Description = string.Empty;
    }

    internal RecurringScheduleLineItem(Guid recurringScheduleId, string description, decimal quantity, decimal unitPrice, Guid? productId)
    {
        RecurringScheduleId = Guard.AgainstEmpty(recurringScheduleId, nameof(recurringScheduleId));
        Description = Guard.AgainstNullOrWhiteSpace(description, nameof(description));
        Quantity = Guard.AgainstNegativeOrZero(quantity, nameof(quantity));
        UnitPrice = Guard.AgainstNegative(unitPrice, nameof(unitPrice));
        ProductId = productId;
    }
}
