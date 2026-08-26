namespace SalesDesk.Application.Admin;

public sealed class PlatformMetricsDto
{
    public int TotalWorkspaces { get; init; }

    public int TotalActiveWorkspaces { get; init; }

    public int TotalUsers { get; init; }

    public int TotalIssuedDocuments { get; init; }

    /// <summary>
    /// Documents issued so far as a percentage of the combined document quota across
    /// every active workspace that has one set. There is no billing/subscription
    /// system anywhere in this app, so this stands in for "Platform MRR/Quota usage"
    /// honestly — it's real quota utilization, not a fabricated revenue figure. Null
    /// when no active workspace has a quota configured (nothing to divide by).
    /// </summary>
    public decimal? DocumentQuotaUsagePercent { get; init; }

    /// <summary>"Healthy" if every metric above was computed successfully against the
    /// database; "Unhealthy" if the query itself failed.</summary>
    public string SystemHealth { get; init; } = string.Empty;
}

public sealed class WorkspaceSummaryDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int? DocumentQuota { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public int UserCount { get; init; }

    public int DocumentCount { get; init; }
}

/// <summary>A row in the admin console's platform-wide Users directory.</summary>
public sealed class AdminUserDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public Guid WorkspaceId { get; init; }

    public string WorkspaceName { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class AuditLogEntryDto
{
    public Guid Id { get; init; }

    public string EventType { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public Guid? WorkspaceId { get; init; }

    public Guid? UserId { get; init; }

    public DateTime OccurredAtUtc { get; init; }
}

public sealed class PagedResult<T>
{
    public List<T> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
