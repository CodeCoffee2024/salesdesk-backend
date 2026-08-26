using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Templates;

/// <summary>
/// A reusable visual layout that a quote or invoice can be rendered with.
/// </summary>
public sealed class Template : Entity
{
    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public TemplateTargetType TargetType { get; private set; }

    public string? AccentColor { get; private set; }

    public bool IsDefault { get; private set; }

    public int UsageCount { get; private set; }

    private Template()
    {
        Name = string.Empty;
    }

    public Template(
        Guid workspaceId,
        string name,
        TemplateTargetType targetType = TemplateTargetType.QuotesAndInvoices,
        string? description = null,
        string? accentColor = null,
        bool isDefault = false)
    {
        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        TargetType = targetType;
        Description = description;
        AccentColor = accentColor;
        IsDefault = isDefault;
        UsageCount = 0;
    }

    public void UpdateDetails(string name, TemplateTargetType targetType, string? description, string? accentColor)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        TargetType = targetType;
        Description = description;
        AccentColor = accentColor;
    }

    public void MarkAsDefault() => IsDefault = true;

    public void UnmarkAsDefault() => IsDefault = false;

    public void RecordUsage() => UsageCount++;
}
