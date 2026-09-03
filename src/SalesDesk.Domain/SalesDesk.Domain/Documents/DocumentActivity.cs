using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Documents;

/// <summary>
/// One entry in a document's history — "what happened and when," for the
/// activity timeline both the workspace (document preview) and the client
/// (public view) can see. Append-only: nothing here is ever edited or removed,
/// unlike the single-slot fields on <see cref="Document"/> itself (e.g.
/// RevisionFeedback, which is overwritten by the next revision request) that
/// can only ever describe the most recent occurrence of something.
/// </summary>
public sealed class DocumentActivity : Entity
{
    public Guid DocumentId { get; private set; }

    public Document? Document { get; private set; }

    public DocumentActivityType Type { get; private set; }

    /// <summary>Optional human-readable context — revision feedback text, the signer's name, the status set. Never carries anything a client shouldn't see (IP addresses, internal notes) — see PublicDocumentMapper for what's actually exposed publicly.</summary>
    public string? Detail { get; private set; }

    // Plain DateTime (Kind=Utc), matching AuditLog — ORDER BY on a
    // DateTimeOffset column isn't supported by the SQLite provider the handler
    // tests run against, and this timestamp is always UTC by definition anyway.
    public DateTime OccurredAtUtc { get; private set; }

    private DocumentActivity()
    {
    }

    public DocumentActivity(Guid documentId, DocumentActivityType type, string? detail, DateTime occurredAtUtc)
    {
        DocumentId = Guard.AgainstEmpty(documentId, nameof(documentId));
        Type = type;
        Detail = detail;
        OccurredAtUtc = occurredAtUtc;
    }
}
