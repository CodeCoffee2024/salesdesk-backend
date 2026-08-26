using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Templates;

public sealed class TemplateDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public TemplateTargetType TargetType { get; init; }

    public string? AccentColor { get; init; }

    public string? ContentHtml { get; init; }

    public bool IsDefault { get; init; }

    public int UsageCount { get; init; }
}
