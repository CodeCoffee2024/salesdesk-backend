using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents.Public;

/// <summary>
/// What an anonymous client sees at the public document link (TASK-023/024) —
/// deliberately narrower than <see cref="DocumentDto"/>: no internal Id, WorkspaceId
/// or TemplateId, since those never need to leave the authenticated app.
/// </summary>
public sealed class PublicDocumentDto
{
    public string DocumentNumber { get; init; } = string.Empty;

    public DocumentType Type { get; init; }

    public DocumentStatus Status { get; init; }

    public DateOnly IssueDate { get; init; }

    public DateOnly DueDate { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerCompany { get; init; } = string.Empty;

    public string WorkspaceName { get; init; } = string.Empty;

    public string? WorkspaceLogoUrl { get; init; }

    public decimal Subtotal { get; init; }

    public decimal Total { get; init; }

    public List<DocumentLineItemDto> LineItems { get; init; } = [];

    public bool IsSigned { get; init; }

    public string? SignedByName { get; init; }

    public DateTimeOffset? SignedAtUtc { get; init; }
}
