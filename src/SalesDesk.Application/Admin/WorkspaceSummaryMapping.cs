using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Admin;

/// <summary>Shared by every admin command that returns the workspace it just changed.</summary>
internal static class WorkspaceSummaryMapping
{
    public static async Task<WorkspaceSummaryDto> ToSummaryAsync(this Workspace workspace, IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var userCount = await context.Users.CountAsync(u => u.WorkspaceId == workspace.Id, cancellationToken);
        var documentCount = await context.Documents.CountAsync(d => d.WorkspaceId == workspace.Id, cancellationToken);

        return new WorkspaceSummaryDto
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Email = workspace.Email,
            IsActive = workspace.IsActive,
            DocumentQuota = workspace.DocumentQuota,
            CreatedAt = workspace.CreatedAt,
            UserCount = userCount,
            DocumentCount = documentCount
        };
    }
}
