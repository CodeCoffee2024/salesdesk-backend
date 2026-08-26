using FluentAssertions;
using SalesDesk.Domain.Products;

namespace SalesDesk.Domain.Tests;

public class ProductTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    [Fact]
    public void Constructor_sets_all_provided_values()
    {
        var product = new Product(WorkspaceId, "Brand identity sprint", 4200m, ProductUnit.Project, "Strategy and identity kit.", "Branding");

        product.Name.Should().Be("Brand identity sprint");
        product.Price.Should().Be(4200m);
        product.Unit.Should().Be(ProductUnit.Project);
        product.Description.Should().Be("Strategy and identity kit.");
        product.Category.Should().Be("Branding");
    }

    [Fact]
    public void Constructor_allows_description_and_category_to_be_omitted()
    {
        var product = new Product(WorkspaceId, "SEO Audit", 750m, ProductUnit.Project);

        product.Description.Should().BeNull();
        product.Category.Should().BeNull();
    }

    [Fact]
    public void Constructor_rejects_a_blank_name()
    {
        var act = () => new Product(WorkspaceId, "", 750m, ProductUnit.Project);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_rejects_a_negative_price()
    {
        var act = () => new Product(WorkspaceId, "SEO Audit", -1m, ProductUnit.Project);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_allows_a_zero_price()
    {
        var act = () => new Product(WorkspaceId, "Complimentary consult", 0m, ProductUnit.Hour);

        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateDetails_replaces_all_editable_fields()
    {
        var product = new Product(WorkspaceId, "SEO Audit", 750m, ProductUnit.Project);

        product.UpdateDetails("SEO Audit (extended)", 950m, ProductUnit.Day, "Now includes competitor analysis.", "Marketing");

        product.Name.Should().Be("SEO Audit (extended)");
        product.Price.Should().Be(950m);
        product.Unit.Should().Be(ProductUnit.Day);
        product.Description.Should().Be("Now includes competitor analysis.");
        product.Category.Should().Be("Marketing");
    }
}
