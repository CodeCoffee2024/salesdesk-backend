using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Documents;

/// <summary>
/// Records that a given automated reminder (TASK-025) was already dispatched for a
/// document, so the periodic dispatch engine never sends the same notice twice — at
/// most one row per (DocumentId, Type) pair. Also the source of truth for the
/// "max 1 email per 24-hour window per client" suppression rule: before sending any
/// reminder, the engine checks whether the target customer has any log row with
/// <see cref="SentAtUtc"/> in the last 24 hours, across all of that customer's documents.
/// </summary>
public sealed class DocumentReminderLog : Entity
{
    public Guid DocumentId { get; private set; }

    public Document? Document { get; private set; }

    public ReminderType Type { get; private set; }

    // Plain DateTime (Kind=Utc) rather than DateTimeOffset — see AuditLog.OccurredAtUtc
    // for why: this is always UTC by definition, and the dispatch engine's 24-hour
    // suppression window needs a >= comparison the SQLite provider these handlers
    // are unit-tested against can't translate for DateTimeOffset.
    public DateTime SentAtUtc { get; private set; }

    private DocumentReminderLog()
    {
    }

    public DocumentReminderLog(Guid documentId, ReminderType type, DateTime sentAtUtc)
    {
        DocumentId = Guard.AgainstEmpty(documentId, nameof(documentId));
        Type = type;
        SentAtUtc = sentAtUtc;
    }
}
