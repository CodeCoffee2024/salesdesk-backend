namespace SalesDesk.Application.Workspaces;

public sealed class WorkspaceProfileDto
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Tagline { get; init; }

    public string? Address { get; init; }

    public string? LogoUrl { get; init; }
}
