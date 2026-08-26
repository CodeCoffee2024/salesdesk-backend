using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Admin;

/// <summary>Platform-wide Users directory, for the admin console's "see what's inside
/// other users" view. Deliberately queries across every workspace, same as the other
/// admin handlers — this is the platform-operator surface, not tenant business data.</summary>
public sealed record GetUsersQuery(string? Search, Guid? WorkspaceId) : IRequest<List<AdminUserDto>>;

public sealed class GetUsersQueryHandler(IApplicationDbContext context) : IRequestHandler<GetUsersQuery, List<AdminUserDto>>
{
    public async Task<List<AdminUserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var query =
            from user in context.Users
            join workspace in context.Workspaces on user.WorkspaceId equals workspace.Id
            select new { user, workspace };

        if (request.WorkspaceId.HasValue)
        {
            query = query.Where(x => x.user.WorkspaceId == request.WorkspaceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // ToLower().Contains() rather than EF.Functions.ILike — see
            // GetWorkspacesQuery for why (translates on both Npgsql and the SQLite
            // test fixture, where ILike isn't supported).
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.user.Email.ToLower().Contains(term) || x.user.FullName.ToLower().Contains(term));
        }

        // Ordered client-side rather than via OrderByDescending(CreatedAt) in the
        // query: CreatedAt is a DateTimeOffset column, and ORDER BY on that type
        // isn't supported by the SQLite provider this app's handler tests run
        // against (see AuditLog.OccurredAtUtc's comment for the same constraint).
        var users = await query
            .Select(x => new AdminUserDto
            {
                Id = x.user.Id,
                Email = x.user.Email,
                FullName = x.user.FullName,
                Role = x.user.Role.ToString(),
                WorkspaceId = x.user.WorkspaceId,
                WorkspaceName = x.workspace.Name,
                CreatedAt = x.user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return users.OrderByDescending(u => u.CreatedAt).ToList();
    }
}
