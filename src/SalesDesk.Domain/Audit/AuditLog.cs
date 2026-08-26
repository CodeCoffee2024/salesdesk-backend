using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Audit;

/// <summary>
/// A record of a critical platform event, surfaced in the System Admin Console's
/// Global Audit Log (TASK-017 AC4). <see cref="WorkspaceId"/> and <see cref="UserId"/>
/// are nullable because some events — a SystemError from an unhandled exception, for
/// instance — have no tenant or actor to attribute them to.
/// </summary>
public sealed class AuditLog : Entity
{
    public string EventType { get; private set; }

    public string Message { get; private set; }

    public Guid? WorkspaceId { get; private set; }

    public Guid? UserId { get; private set; }

    // Plain DateTime (Kind=Utc) rather than DateTimeOffset: this timestamp is
    // always UTC by definition (see the name), and ORDER BY on a DateTimeOffset
    // column isn't supported by the SQLite provider this app's handler tests run
    // against — DateTime avoids that entirely without losing any information this
    // entity actually needs.
    public DateTime OccurredAtUtc { get; private set; }

    private AuditLog()
    {
        EventType = string.Empty;
        Message = string.Empty;
    }

    public AuditLog(string eventType, string message, Guid? workspaceId, Guid? userId)
    {
        EventType = Guard.AgainstNullOrWhiteSpace(eventType, nameof(eventType));
        Message = Guard.AgainstNullOrWhiteSpace(message, nameof(message));
        WorkspaceId = workspaceId;
        UserId = userId;
        OccurredAtUtc = DateTime.UtcNow;
    }
}
