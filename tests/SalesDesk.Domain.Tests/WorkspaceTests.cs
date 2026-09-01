using FluentAssertions;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Domain.Tests;

public class WorkspaceTests
{
    [Fact]
    public void Constructor_sets_all_provided_values()
    {
        var workspace = new Workspace(
            "Northline",
            "hello@northline.studio",
            tagline: "Creative studio",
            address: "14 Rizal Avenue, Makati, Metro Manila",
            logoUrl: "https://cdn.example.com/northline-logo.png");

        workspace.Name.Should().Be("Northline");
        workspace.Email.Should().Be("hello@northline.studio");
        workspace.Tagline.Should().Be("Creative studio");
        workspace.Address.Should().Be("14 Rizal Avenue, Makati, Metro Manila");
        workspace.LogoUrl.Should().Be("https://cdn.example.com/northline-logo.png");
    }

    [Fact]
    public void Constructor_allows_optional_fields_to_be_omitted()
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");

        workspace.Tagline.Should().BeNull();
        workspace.Address.Should().BeNull();
        workspace.LogoUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("", "hello@northline.studio")]
    [InlineData("Northline", "")]
    public void Constructor_rejects_blank_required_fields(string name, string email)
    {
        var act = () => new Workspace(name, email);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProfile_replaces_all_fields_without_changing_identity()
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var originalId = workspace.Id;

        workspace.UpdateProfile("Northline Studio", "contact@northline.studio", "Brand & web", "New address", "https://cdn.example.com/logo2.png", "DE", "EUR");

        workspace.Id.Should().Be(originalId);
        workspace.Name.Should().Be("Northline Studio");
        workspace.Email.Should().Be("contact@northline.studio");
        workspace.Tagline.Should().Be("Brand & web");
        workspace.Address.Should().Be("New address");
        workspace.LogoUrl.Should().Be("https://cdn.example.com/logo2.png");
        workspace.Country.Should().Be("DE");
        workspace.DefaultCurrency.Should().Be("EUR");
    }

    [Fact]
    public void Constructor_defaults_country_and_currency_to_US_and_USD()
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");

        workspace.Country.Should().Be("US");
        workspace.DefaultCurrency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_normalizes_country_and_currency_to_uppercase()
    {
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "ph", defaultCurrency: "php");

        workspace.Country.Should().Be("PH");
        workspace.DefaultCurrency.Should().Be("PHP");
    }

    [Theory]
    [InlineData("USA")]
    [InlineData("U")]
    [InlineData("12")]
    public void Constructor_rejects_a_malformed_country_code(string country)
    {
        var act = () => new Workspace("Northline", "hello@northline.studio", country: country);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("123")]
    public void Constructor_rejects_a_malformed_currency_code(string currency)
    {
        var act = () => new Workspace("Northline", "hello@northline.studio", defaultCurrency: currency);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_defaults_to_active_with_a_100_document_quota()
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");

        workspace.IsActive.Should().BeTrue();
        workspace.DocumentQuota.Should().Be(100);
    }

    [Fact]
    public void Suspend_then_Activate_toggles_IsActive()
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");

        workspace.Suspend();
        workspace.IsActive.Should().BeFalse();

        workspace.Activate();
        workspace.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetDocumentQuota_accepts_null_for_unlimited()
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");

        workspace.SetDocumentQuota(null);

        workspace.DocumentQuota.Should().BeNull();
    }

    [Fact]
    public void SetDocumentQuota_rejects_a_negative_value()
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");

        var act = () => workspace.SetDocumentQuota(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
