using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Billing;

/// <summary>TASK-038: backs GET /api/workspace/billing/pricing — the priced tier catalog for the current workspace's own region, driven by its self-reported Country (see PricingCatalog).</summary>
public sealed record GetPricingCatalogQuery : IRequest<PricingCatalogDto>;

public sealed class GetPricingCatalogQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<GetPricingCatalogQuery, PricingCatalogDto>
{
    public async Task<PricingCatalogDto> Handle(GetPricingCatalogQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var workspace = await context.Workspaces.SingleAsync(w => w.Id == workspaceId, cancellationToken);

        return PricingCatalog.ForCountry(workspace.Country);
    }
}
