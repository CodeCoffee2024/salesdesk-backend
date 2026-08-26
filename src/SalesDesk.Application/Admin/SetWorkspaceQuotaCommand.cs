using FluentValidation;
using MediatR;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Admin;

/// <summary>Adjust a tenant's document quota limit — TASK-017 AC3. Null quota means unlimited.</summary>
public sealed record SetWorkspaceQuotaCommand(Guid WorkspaceId, int? DocumentQuota) : IRequest<WorkspaceSummaryDto>;

public sealed class SetWorkspaceQuotaCommandValidator : AbstractValidator<SetWorkspaceQuotaCommand>
{
    public SetWorkspaceQuotaCommandValidator()
    {
        RuleFor(c => c.DocumentQuota!.Value).GreaterThanOrEqualTo(0).When(c => c.DocumentQuota.HasValue);
    }
}

public sealed class SetWorkspaceQuotaCommandHandler(IApplicationDbContext context, IAuditLogger auditLogger, ICurrentUserService currentUser)
    : IRequestHandler<SetWorkspaceQuotaCommand, WorkspaceSummaryDto>
{
    public async Task<WorkspaceSummaryDto> Handle(SetWorkspaceQuotaCommand request, CancellationToken cancellationToken)
    {
        var workspace = await context.Workspaces.FindAsync([request.WorkspaceId], cancellationToken)
            ?? throw new NotFoundException(nameof(Workspace), request.WorkspaceId);

        workspace.SetDocumentQuota(request.DocumentQuota);
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditEventTypes.WorkspaceQuotaChanged,
            $"Workspace \"{workspace.Name}\" document quota set to {(request.DocumentQuota?.ToString() ?? "unlimited")}.",
            workspace.Id,
            currentUser.UserId,
            cancellationToken);

        return await workspace.ToSummaryAsync(context, cancellationToken);
    }
}
