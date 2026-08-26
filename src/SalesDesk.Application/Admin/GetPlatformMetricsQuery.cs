using MediatR;
using Microsoft.EntityFrameworkCore;
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

            var activeWorkspacesWithQuota = await context.Workspaces
                .Where(w => w.IsActive && w.DocumentQuota != null)
                .Select(w => new { w.Id, Quota = w.DocumentQuota!.Value })
                .ToListAsync(cancellationToken);

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
