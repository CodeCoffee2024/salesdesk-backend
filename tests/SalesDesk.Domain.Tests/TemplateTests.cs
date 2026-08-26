using FluentAssertions;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Domain.Tests;

public class TemplateTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    [Fact]
    public void Constructor_defaults_to_QuotesAndInvoices_not_default_and_zero_usage()
    {
        var template = new Template(WorkspaceId, "Studio Standard");

        template.TargetType.Should().Be(TemplateTargetType.QuotesAndInvoices);
        template.IsDefault.Should().BeFalse();
        template.UsageCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_rejects_a_blank_name()
    {
        var act = () => new Template(WorkspaceId, "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordUsage_increments_the_usage_count_each_call()
    {
        var template = new Template(WorkspaceId, "Friendly Quote", TemplateTargetType.QuotesOnly);

        template.RecordUsage();
        template.RecordUsage();
        template.RecordUsage();

        template.UsageCount.Should().Be(3);
    }

    [Fact]
    public void MarkAsDefault_and_UnmarkAsDefault_toggle_the_flag()
    {
        var template = new Template(WorkspaceId, "Studio Standard");

        template.MarkAsDefault();
        template.IsDefault.Should().BeTrue();

        template.UnmarkAsDefault();
        template.IsDefault.Should().BeFalse();
    }
}
