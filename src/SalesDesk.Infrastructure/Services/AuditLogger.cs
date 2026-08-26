using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;

namespace SalesDesk.Infrastructure.Services;

public sealed class AuditLogger(IApplicationDbContext context) : IAuditLogger
{
    public async Task LogAsync(string eventType, string message, Guid? workspaceId, Guid? userId, CancellationToken cancellationToken)
    {
        context.AuditLogs.Add(new AuditLog(eventType, message, workspaceId, userId));
        await context.SaveChangesAsync(cancellationToken);
    }
}
