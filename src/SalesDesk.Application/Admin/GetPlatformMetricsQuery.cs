using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Billing;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Admin;

public sealed record GetPlatformMetricsQuery : IRequest<PlatformMetricsDto>;

/// <summary>System Admin Console dashboard metrics — TASK-017 AC2.</summary>
public sealed class GetPlatformMetricsQueryHandler(IApplicationDbContext context) : IRequestHandler<GetPlatformMetricsQuery, PlatformMetricsDto>
{
    public async Task<PlatformMetricsDto> Handle(GetPlatformMetricsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var totalWorkspaces = await context.Workspaces.CountAsync(cancellationToken);
            var totalActiveWorkspaces = await context.Workspaces.CountAsync(w => w.IsActive, cancellationToken);
            var totalUsers = await context.Users.CountAsync(cancellationToken);
            var totalIssuedDocuments = await context.Documents.CountAsync(cancellationToken);

            // Since the quota-reconciliation fix, most workspaces carry no explicit
            // DocumentQuota override (null defers to their subscription tier's own
            // limit — see Workspace.DocumentQuota) — using only the raw column here
            // would make this metric go quiet for almost every workspace. Resolve
            // each workspace's *effective* limit (override, else tier default) instead,
            // same as CreateDocumentCommand does, and still exclude anything that
            // resolves to unlimited (no tier cap and no override).
            var activeWorkspaces = await context.Workspaces
                .Where(w => w.IsActive)
                .Select(w => new { w.Id, w.DocumentQuota, w.SubscriptionTier })
                .ToListAsync(cancellationToken);

            var activeWorkspacesWithQuota = activeWorkspaces
                .Select(w => new { w.Id, Quota = w.DocumentQuota ?? PricingCatalog.MonthlyDocumentLimit(w.SubscriptionTier) })
                .Where(w => w.Quota is not null)
                .Select(w => new { w.Id, Quota = w.Quota!.Value })
                .ToList();

            decimal? quotaUsagePercent = null;
            if (activeWorkspacesWithQuota.Count > 0)
            {
                var totalQuota = activeWorkspacesWithQuota.Sum(w => w.Quota);
                var quotedWorkspaceIds = activeWorkspacesWithQuota.Select(w => w.Id).ToList();
                var issuedAgainstQuota = await context.Documents.CountAsync(d => quotedWorkspaceIds.Contains(d.WorkspaceId), cancellationToken);

                quotaUsagePercent = totalQuota == 0 ? 0m : Math.Round(issuedAgainstQuota * 100m / totalQuota, 1);
            }

            return new PlatformMetricsDto
            {
                TotalWorkspaces = totalWorkspaces,
                TotalActiveWorkspaces = totalActiveWorkspaces,
                TotalUsers = totalUsers,
                TotalIssuedDocuments = totalIssuedDocuments,
                DocumentQuotaUsagePercent = quotaUsagePercent,
                SystemHealth = "Healthy"
            };
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A real connectivity/query failure surfaces as "Unhealthy" rather than
            // a 500 — this endpoint IS the health signal, so it shouldn't itself blow
            // up when the thing it's reporting on is down.
            return new PlatformMetricsDto { SystemHealth = "Unhealthy" };
        }
    }
}
