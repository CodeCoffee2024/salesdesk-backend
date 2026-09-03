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

    /// <summary>ISO 4217 code the client portal formats amounts in via Intl.NumberFormat (TASK-029).</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Optional ISO 3166-1 alpha-2 target country, used to pick the display locale alongside Currency (TASK-029).</summary>
    public string? ClientCountry { get; init; }

    public List<DocumentLineItemDto> LineItems { get; init; } = [];

    public bool IsSigned { get; init; }

    public string? SignedByName { get; init; }

    public DateTimeOffset? SignedAtUtc { get; init; }

    /// <summary>PNG data URL of the client's own e-signature — safe to hand back on this same public link, since it's the client viewing what they themselves signed.</summary>
    public string? SignatureImageDataUrl { get; init; }

    /// <summary>The client-facing slice of the document's timeline, oldest first — see PublicDocumentMapper for exactly what's included/excluded.</summary>
    public List<PublicDocumentActivityDto> Timeline { get; init; } = [];
}

public sealed class PublicDocumentActivityDto
{
    public DocumentActivityType Type { get; init; }

    public string? Detail { get; init; }

    public DateTime OccurredAtUtc { get; init; }
}
