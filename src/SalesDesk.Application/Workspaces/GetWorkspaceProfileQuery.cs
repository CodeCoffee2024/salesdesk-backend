using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Workspaces;

/// <summary>Backs GET /api/workspace/profile — the current user's own workspace details (name, logo, contact info), editable from a workspace settings page.</summary>
public sealed record GetWorkspaceProfileQuery : IRequest<WorkspaceProfileDto>;

public sealed class GetWorkspaceProfileQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<GetWorkspaceProfileQuery, WorkspaceProfileDto>
{
    public async Task<WorkspaceProfileDto> Handle(GetWorkspaceProfileQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var workspace = await context.Workspaces.SingleAsync(w => w.Id == workspaceId, cancellationToken);

        return new WorkspaceProfileDto
        {
            Name = workspace.Name,
            Email = workspace.Email,
            Tagline = workspace.Tagline,
            Address = workspace.Address,
            LogoUrl = workspace.LogoUrl,
            Country = workspace.Country,
            DefaultCurrency = workspace.DefaultCurrency
        };
    }
}
