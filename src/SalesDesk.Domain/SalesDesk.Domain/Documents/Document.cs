using SalesDesk.Domain.Common;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Domain.Documents;

/// <summary>
/// A quote or invoice issued to a customer. The aggregate root for its
/// <see cref="DocumentLineItem"/> collection — line items are only ever added,
/// updated or removed through this type, which keeps <see cref="Subtotal"/> and
/// <see cref="Total"/> consistent with the items they're computed from.
/// </summary>
public sealed class Document : Entity
{
    private readonly List<DocumentLineItem> _lineItems = [];

    public Guid WorkspaceId { get; private set; }

    public string DocumentNumber { get; private set; }

    public DocumentType Type { get; private set; }

    public DocumentStatus Status { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public DateOnly DueDate { get; private set; }

    public Guid CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public Guid TemplateId { get; private set; }

    public Template? Template { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal Total { get; private set; }

    public IReadOnlyCollection<DocumentLineItem> LineItems => _lineItems.AsReadOnly();

    private Document()
    {
        DocumentNumber = string.Empty;
    }

    public Document(Guid workspaceId, string documentNumber, DocumentType type, Guid customerId, Guid templateId, DateOnly issueDate, DateOnly dueDate)
    {
        if (dueDate < issueDate)
        {
            throw new ArgumentOutOfRangeException(nameof(dueDate), dueDate, "Due date cannot be earlier than the issue date.");
        }

        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        DocumentNumber = Guard.AgainstNullOrWhiteSpace(documentNumber, nameof(documentNumber));
        Type = type;
        CustomerId = Guard.AgainstEmpty(customerId, nameof(customerId));
        TemplateId = Guard.AgainstEmpty(templateId, nameof(templateId));
        IssueDate = issueDate;
        DueDate = dueDate;
        Status = DocumentStatus.Draft;
    }

    public DocumentLineItem AddLineItem(string description, decimal quantity, decimal unitPrice, Guid? productId = null)
    {
        var lineItem = new DocumentLineItem(Id, description, quantity, unitPrice, productId);
        _lineItems.Add(lineItem);
        RecalculateTotals();
        return lineItem;
    }

    public void UpdateLineItem(Guid lineItemId, string description, decimal quantity, decimal unitPrice, Guid? productId = null)
    {
        var lineItem = _lineItems.SingleOrDefault(li => li.Id == lineItemId)
            ?? throw new InvalidOperationException($"Line item '{lineItemId}' does not belong to document '{Id}'.");

        lineItem.UpdateDetails(description, quantity, unitPrice, productId);
        RecalculateTotals();
    }

    public void RemoveLineItem(Guid lineItemId)
    {
        var lineItem = _lineItems.SingleOrDefault(li => li.Id == lineItemId)
            ?? throw new InvalidOperationException($"Line item '{lineItemId}' does not belong to document '{Id}'.");

        _lineItems.Remove(lineItem);
        RecalculateTotals();
    }

    public void ChangeStatus(DocumentStatus status) => Status = status;

    public void Reschedule(DateOnly dueDate)
    {
        if (dueDate < IssueDate)
        {
            throw new ArgumentOutOfRangeException(nameof(dueDate), dueDate, "Due date cannot be earlier than the issue date.");
        }

        DueDate = dueDate;
    }

    public void ChangeTemplate(Guid templateId) => TemplateId = Guard.AgainstEmpty(templateId, nameof(templateId));

    /// <summary>
    /// Replaces the entire line-item set in one operation — the shape a full (PUT)
    /// update needs, as opposed to the incremental Add/Update/Remove methods above.
    /// </summary>
    public void ReplaceLineItems(IEnumerable<NewLineItem> items)
    {
        _lineItems.Clear();

        foreach (var item in items)
        {
            _lineItems.Add(new DocumentLineItem(Id, item.Description, item.Quantity, item.UnitPrice, item.ProductId));
        }

        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        Subtotal = _lineItems.Sum(li => li.LineTotal);
        // No tax/discount modeling yet, so the total mirrors the subtotal.
        Total = Subtotal;
    }
}
