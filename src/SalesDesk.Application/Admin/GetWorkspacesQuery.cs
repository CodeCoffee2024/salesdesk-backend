using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Admin;

/// <summary>Workspace directory — TASK-017 AC3. Deliberately queries across every
/// workspace, unlike the workspace-scoped handlers elsewhere in this app: this is
/// the platform-operator surface, not tenant business data.</summary>
public sealed record GetWorkspacesQuery(string? Search) : IRequest<List<WorkspaceSummaryDto>>;

public sealed class GetWorkspacesQueryHandler(IApplicationDbContext context) : IRequestHandler<GetWorkspacesQuery, List<WorkspaceSummaryDto>>
{
    public async Task<List<WorkspaceSummaryDto>> Handle(GetWorkspacesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Workspaces.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // ToLower().Contains() rather than EF.Functions.ILike: translates on
            // both the Npgsql provider (production) and SQLite (this project's
            // in-memory handler test fixture), where ILike isn't supported.
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(w => w.Name.ToLower().Contains(term) || w.Email.ToLower().Contains(term));
        }

        var workspaces = await query.OrderBy(w => w.Name).ToListAsync(cancellationToken);
        var workspaceIds = workspaces.Select(w => w.Id).ToList();

        var userCounts = await context.Users
            .Where(u => workspaceIds.Contains(u.WorkspaceId))
            .GroupBy(u => u.WorkspaceId)
            .Select(g => new { WorkspaceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.WorkspaceId, g => g.Count, cancellationToken);

        var documentCounts = await context.Documents
            .Where(d => workspaceIds.Contains(d.WorkspaceId))
            .GroupBy(d => d.WorkspaceId)
            .Select(g => new { WorkspaceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.WorkspaceId, g => g.Count, cancellationToken);

        return workspaces.Select(w => new WorkspaceSummaryDto
        {
            Id = w.Id,
            Name = w.Name,
            Email = w.Email,
            IsActive = w.IsActive,
            DocumentQuota = w.DocumentQuota,
            CreatedAt = w.CreatedAt,
            UserCount = userCounts.GetValueOrDefault(w.Id),
            DocumentCount = documentCounts.GetValueOrDefault(w.Id)
        }).ToList();
    }
}
