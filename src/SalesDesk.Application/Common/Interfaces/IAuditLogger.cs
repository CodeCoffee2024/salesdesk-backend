namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// Writes an entry to the platform's audit trail (TASK-017 AC4). Implemented in
/// Infrastructure against <see cref="IApplicationDbContext"/>.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(string eventType, string message, Guid? workspaceId, Guid? userId, CancellationToken cancellationToken);
}
