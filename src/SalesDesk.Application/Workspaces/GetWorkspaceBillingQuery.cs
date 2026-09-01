using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Workspaces;

/// <summary>TASK-031: backs GET /api/workspace/billing — the current user's own workspace subscription tier and early-bird promo status, for the /settings/billing page.</summary>
public sealed record GetWorkspaceBillingQuery : IRequest<WorkspaceBillingDto>;

public sealed class GetWorkspaceBillingQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<GetWorkspaceBillingQuery, WorkspaceBillingDto>
{
    public async Task<WorkspaceBillingDto> Handle(GetWorkspaceBillingQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var workspace = await context.Workspaces.SingleAsync(w => w.Id == workspaceId, cancellationToken);

        return new WorkspaceBillingDto
        {
            SubscriptionTier = workspace.SubscriptionTier.ToString(),
            SubscriptionEndDate = workspace.SubscriptionEndDate,
            IsEarlyBirdPromo = workspace.IsEarlyBirdPromo
        };
    }
}
