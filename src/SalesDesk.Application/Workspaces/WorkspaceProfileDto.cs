namespace SalesDesk.Application.Workspaces;

public sealed class WorkspaceProfileDto
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Tagline { get; init; }

    public string? Address { get; init; }

    public string? LogoUrl { get; init; }

    /// <summary>ISO 3166-1 alpha-2 operating country (TASK-029).</summary>
    public string Country { get; init; } = "US";

    /// <summary>ISO 4217 default currency new documents are priced in unless overridden (TASK-029).</summary>
    public string DefaultCurrency { get; init; } = "USD";

    /// <summary>IANA time zone id document/reminder email timestamps are localized into instead of raw UTC.</summary>
    public string TimeZoneId { get; init; } = "UTC";
}
