using SalesDesk.Domain.Common;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Domain.Documents;

/// <summary>
/// A "repeat this document every [interval]" retainer schedule (VERSION-2 roadmap
/// item 3). Not a document itself — a template the background generator
/// (<c>GenerateDueRecurringDocumentsCommand</c>) reads from to create a new Draft
/// <see cref="Document"/> each time <see cref="NextRunDate"/> comes due, copying its
/// line items onto the new document and then advancing the schedule.
/// </summary>
public sealed class RecurringSchedule : Entity
{
    private readonly List<RecurringScheduleLineItem> _lineItems = [];

    public Guid WorkspaceId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public Guid TemplateId { get; private set; }

    public Template? Template { get; private set; }

    public DocumentType Type { get; private set; }

    /// <summary>ISO 4217 code new documents are issued in — same override shape as Document.Currency (TASK-029).</summary>
    public string Currency { get; private set; }

    public string? ClientCountry { get; private set; }

    /// <summary>Days after each generated document's issue date its due date is set to.</summary>
    public int DueDateOffsetDays { get; private set; }

    public RecurrenceInterval Interval { get; private set; }

    /// <summary>The next date this schedule is due to generate a document. Advanced by <see cref="RecordRun"/> after each generation.</summary>
    public DateOnly NextRunDate { get; private set; }

    /// <summary>True dispatches each generated document to the client immediately; false leaves it as a Draft for review first.</summary>
    public bool AutoDispatch { get; private set; }

    /// <summary>Paused schedules are skipped by the generator but keep their NextRunDate, so resuming picks up from where it left off (and immediately catches up if that date has since passed).</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<RecurringScheduleLineItem> LineItems => _lineItems.AsReadOnly();

    private RecurringSchedule()
    {
        Currency = "USD";
    }

    public RecurringSchedule(
        Guid workspaceId,
        Guid customerId,
        Guid templateId,
        DocumentType type,
        DateOnly startDate,
        RecurrenceInterval interval,
        int dueDateOffsetDays,
        bool autoDispatch,
        string currency = "USD",
        string? clientCountry = null)
    {
        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        CustomerId = Guard.AgainstEmpty(customerId, nameof(customerId));
        TemplateId = Guard.AgainstEmpty(templateId, nameof(templateId));
        Type = type;

        if (dueDateOffsetDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dueDateOffsetDays), dueDateOffsetDays, "Due date offset cannot be negative.");
        }

        DueDateOffsetDays = dueDateOffsetDays;
        Interval = interval;
        NextRunDate = startDate;
        AutoDispatch = autoDispatch;
        Currency = Guard.AgainstInvalidIsoCode(currency, 3, nameof(currency));
        ClientCountry = Guard.AgainstInvalidIsoCodeOrNull(clientCountry, 2, nameof(clientCountry));
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public RecurringScheduleLineItem AddLineItem(string description, decimal quantity, decimal unitPrice, Guid? productId = null)
    {
        var lineItem = new RecurringScheduleLineItem(Id, description, quantity, unitPrice, productId);
        _lineItems.Add(lineItem);
        return lineItem;
    }

    public void Pause() => IsActive = false;

    public void Resume() => IsActive = true;

    /// <summary>Called by the generator immediately after creating a document for this schedule, advancing it past today so the same period is never generated twice.</summary>
    public void RecordRun(DateOnly nextRunDate) => NextRunDate = nextRunDate;
}
