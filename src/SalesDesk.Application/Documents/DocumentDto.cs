using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents;

public sealed class DocumentLineItemDto
{
    public Guid Id { get; init; }

    public Guid? ProductId { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }
}

/// <summary>
/// Full document representation used for both the filtered list (GET /api/documents)
/// and the dedicated preview fetch (GET /api/documents/{id}) — the latter is what
/// actually requires the nested customer/template/line-item detail this DTO
/// carries, but reusing one shape for both keeps the mapping profile and
/// controller simple at this scale.
/// </summary>
public sealed class DocumentDto
{
    public Guid Id { get; init; }

    public string DocumentNumber { get; init; } = string.Empty;

    public DocumentType Type { get; init; }

    public DocumentStatus Status { get; init; }

    public DateOnly IssueDate { get; init; }

    public DateOnly DueDate { get; init; }

    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerCompany { get; init; } = string.Empty;

    public Guid TemplateId { get; init; }

    public string TemplateName { get; init; } = string.Empty;

    public decimal Subtotal { get; init; }

    public decimal Total { get; init; }

    public List<DocumentLineItemDto> LineItems { get; init; } = [];
}
