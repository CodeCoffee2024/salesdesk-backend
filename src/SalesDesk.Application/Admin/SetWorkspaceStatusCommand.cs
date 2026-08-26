using MediatR;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Admin;

/// <summary>Suspend/activate a tenant — TASK-017 AC3.</summary>
public sealed record SetWorkspaceStatusCommand(Guid WorkspaceId, bool IsActive) : IRequest<WorkspaceSummaryDto>;

public sealed class SetWorkspaceStatusCommandHandler(IApplicationDbContext context, IAuditLogger auditLogger, ICurrentUserService currentUser)
    : IRequestHandler<SetWorkspaceStatusCommand, WorkspaceSummaryDto>
{
    public async Task<WorkspaceSummaryDto> Handle(SetWorkspaceStatusCommand request, CancellationToken cancellationToken)
    {
        var workspace = await context.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new NotFoundException(nameof(Workspace), request.WorkspaceId);

        if (request.IsActive)
        {
            workspace.Activate();
        }
        else
        {
            workspace.Suspend();
        }

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            request.IsActive ? AuditEventTypes.WorkspaceActivated : AuditEventTypes.WorkspaceSuspended,
            $"Workspace \"{workspace.Name}\" was {(request.IsActive ? "activated" : "suspended")}.",
            workspace.Id,
            currentUser.UserId,
            cancellationToken);

        return await workspace.ToSummaryAsync(context, cancellationToken);
    }
}
