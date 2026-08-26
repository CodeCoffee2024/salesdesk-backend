using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Tests;

public sealed class FakeAuditLogger : IAuditLogger
{
    public List<(string EventType, string Message, Guid? WorkspaceId, Guid? UserId)> Entries { get; } = [];

    public Task LogAsync(string eventType, string message, Guid? workspaceId, Guid? userId, CancellationToken cancellationToken)
    {
        Entries.Add((eventType, message, workspaceId, userId));
        return Task.CompletedTask;
    }
}
