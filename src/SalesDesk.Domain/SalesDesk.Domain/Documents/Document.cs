using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>Opaque id used in the unauthenticated public URL (TASK-023/024) — deliberately separate from <see cref="Entity.Id"/> so the internal id never has to appear in a link shared with a client.</summary>
    public Guid PublicToken { get; private set; }

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

    public DocumentSignature? Signature { get; private set; }

    /// <summary>True once a client has e-signed this document — every mutator below refuses to run while this is set (TASK-024 guardrail: no modifications after signing).</summary>
    public bool IsLocked => Signature is not null;

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
        PublicToken = Guid.NewGuid();
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
        EnsureNotLocked();
        var lineItem = new DocumentLineItem(Id, description, quantity, unitPrice, productId);
        _lineItems.Add(lineItem);
        RecalculateTotals();
        return lineItem;
    }

    public void UpdateLineItem(Guid lineItemId, string description, decimal quantity, decimal unitPrice, Guid? productId = null)
    {
        EnsureNotLocked();
        var lineItem = _lineItems.SingleOrDefault(li => li.Id == lineItemId)
            ?? throw new InvalidOperationException($"Line item '{lineItemId}' does not belong to document '{Id}'.");

        lineItem.UpdateDetails(description, quantity, unitPrice, productId);
        RecalculateTotals();
    }

    public void RemoveLineItem(Guid lineItemId)
    {
        EnsureNotLocked();
        var lineItem = _lineItems.SingleOrDefault(li => li.Id == lineItemId)
            ?? throw new InvalidOperationException($"Line item '{lineItemId}' does not belong to document '{Id}'.");

        _lineItems.Remove(lineItem);
        RecalculateTotals();
    }

    public void ChangeStatus(DocumentStatus status)
    {
        EnsureNotLocked();
        Status = status;
    }

    public void Reschedule(DateOnly dueDate)
    {
        EnsureNotLocked();
        if (dueDate < IssueDate)
        {
            throw new ArgumentOutOfRangeException(nameof(dueDate), dueDate, "Due date cannot be earlier than the issue date.");
        }

        DueDate = dueDate;
    }

    public void ChangeTemplate(Guid templateId)
    {
        EnsureNotLocked();
        TemplateId = Guard.AgainstEmpty(templateId, nameof(templateId));
    }

    /// <summary>
    /// Replaces the entire line-item set in one operation — the shape a full (PUT)
    /// update needs, as opposed to the incremental Add/Update/Remove methods above.
    /// </summary>
    public void ReplaceLineItems(IEnumerable<NewLineItem> items)
    {
        EnsureNotLocked();
        _lineItems.Clear();

        foreach (var item in items)
        {
            _lineItems.Add(new DocumentLineItem(Id, item.Description, item.Quantity, item.UnitPrice, item.ProductId));
        }

        RecalculateTotals();
    }

    public void EnsureNotLocked()
    {
        if (IsLocked)
        {
            throw new InvalidOperationException($"Document '{Id}' is locked and cannot be modified after it has been e-signed.");
        }
    }

    /// <summary>
    /// Records a client's e-signature (TASK-024) and transitions the document to
    /// Accepted — signing and acceptance are the same event on the public flow, not
    /// two separate steps. Throws if the document is already signed; a document can
    /// only ever be accepted once.
    /// </summary>
    public DocumentSignature ApplySignature(
        string signerName,
        string signerEmail,
        SignatureType signatureType,
        string signatureImageDataUrl,
        string ipAddress,
        string userAgent,
        DateTimeOffset signedAtUtc)
    {
        EnsureNotLocked();

        var signature = new DocumentSignature(
            Id, signerName, signerEmail, signatureType, signatureImageDataUrl, ipAddress, userAgent, signedAtUtc, ComputeContentHash());

        Signature = signature;
        Status = DocumentStatus.Accepted;
        return signature;
    }

    /// <summary>
    /// SHA-256 digest of the document's priced content, computed fresh at signing
    /// time and stored on the resulting <see cref="DocumentSignature"/> — a durable
    /// proof of exactly what was agreed to, independent of whatever the row looks
    /// like later. Deliberately excludes mutable-after-the-fact fields like Status.
    /// </summary>
    public string ComputeContentHash()
    {
        var builder = new StringBuilder();
        builder.Append(DocumentNumber).Append('|').Append(Type).Append('|')
            .Append(CustomerId).Append('|').Append(TemplateId).Append('|')
            .Append(IssueDate.ToString("O")).Append('|').Append(DueDate.ToString("O")).Append('|')
            .Append(Subtotal.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(Total.ToString(CultureInfo.InvariantCulture));

        foreach (var item in _lineItems.OrderBy(li => li.Id))
        {
            builder.Append('|').Append(item.Id).Append(':').Append(item.Description).Append(':')
                .Append(item.Quantity.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.UnitPrice.ToString(CultureInfo.InvariantCulture));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void RecalculateTotals()
    {
        Subtotal = _lineItems.Sum(li => li.LineTotal);
        // No tax/discount modeling yet, so the total mirrors the subtotal.
        Total = Subtotal;
    }
}
