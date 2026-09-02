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
public sealed class DocumentSignatureSummaryDto
{
    public string SignerName { get; init; } = string.Empty;

    public string SignerEmail { get; init; } = string.Empty;

    public DateTimeOffset SignedAtUtc { get; init; }

    /// <summary>Included here (unlike the public DTO, which never needs it) so the authenticated preview's "Download PDF" can embed the real signature image, not just an audit line.</summary>
    public string SignatureImageDataUrl { get; init; } = string.Empty;
}

public sealed class DocumentDto
{
    public Guid Id { get; init; }

    /// <summary>Token for the unauthenticated public link (TASK-023/024) — the app builds `/view/{PublicToken}` from this, never the internal Id.</summary>
    public Guid PublicToken { get; init; }

    public bool IsLocked { get; init; }

    /// <summary>True once this document has ever been dispatched to the client (TASK-037) — stays true even after Status later moves on.</summary>
    public bool IsDispatched { get; init; }

    public DateTime? DispatchedAt { get; init; }

    public DocumentSignatureSummaryDto? Signature { get; init; }

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

    /// <summary>ISO 4217 code this document is priced in (TASK-029).</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Optional ISO 3166-1 alpha-2 override of the client's target country (TASK-029).</summary>
    public string? ClientCountry { get; init; }

    public List<DocumentLineItemDto> LineItems { get; init; } = [];
}
