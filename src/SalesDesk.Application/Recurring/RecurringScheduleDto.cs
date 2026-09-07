using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Recurring;

public sealed record RecurringScheduleLineItemDto(string Description, decimal Quantity, decimal UnitPrice, Guid? ProductId);

public sealed record RecurringScheduleDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerCompany,
    Guid TemplateId,
    string TemplateName,
    DocumentType Type,
    string Currency,
    string? ClientCountry,
    int DueDateOffsetDays,
    RecurrenceInterval Interval,
    DateOnly NextRunDate,
    bool AutoDispatch,
    bool IsActive,
    DateTimeOffset CreatedAt,
    List<RecurringScheduleLineItemDto> LineItems);

internal static class RecurringScheduleMapping
{
    public static RecurringScheduleDto ToDto(this RecurringSchedule schedule) => new(
        schedule.Id,
        schedule.CustomerId,
        schedule.Customer?.Name ?? string.Empty,
        schedule.Customer?.Company ?? string.Empty,
        schedule.TemplateId,
        schedule.Template?.Name ?? string.Empty,
        schedule.Type,
        schedule.Currency,
        schedule.ClientCountry,
        schedule.DueDateOffsetDays,
        schedule.Interval,
        schedule.NextRunDate,
        schedule.AutoDispatch,
        schedule.IsActive,
        schedule.CreatedAt,
        schedule.LineItems.Select(li => new RecurringScheduleLineItemDto(li.Description, li.Quantity, li.UnitPrice, li.ProductId)).ToList());
}
