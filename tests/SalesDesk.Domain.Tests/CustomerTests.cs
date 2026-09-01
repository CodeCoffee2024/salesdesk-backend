using FluentAssertions;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Domain.Tests;

public class CustomerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    [Fact]
    public void Constructor_sets_all_provided_values_and_stamps_creation_time()
    {
        var before = DateTimeOffset.UtcNow;

        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio", "+1 415 555 0100");

        var after = DateTimeOffset.UtcNow;

        customer.Name.Should().Be("Maya Chen");
        customer.Company.Should().Be("Northstar Studio");
        customer.Email.Should().Be("maya@northstar.studio");
        customer.Phone.Should().Be("+1 415 555 0100");
        customer.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        customer.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_allows_phone_to_be_omitted()
    {
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");

        customer.Phone.Should().BeNull();
    }

    [Fact]
    public void Constructor_allows_country_to_be_omitted()
    {
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");

        customer.Country.Should().BeNull();
    }

    [Fact]
    public void Constructor_normalizes_a_provided_country_to_uppercase()
    {
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio", country: "ph");

        customer.Country.Should().Be("PH");
    }

    [Fact]
    public void Constructor_rejects_a_malformed_country_code()
    {
        var act = () => new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio", country: "PHL");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("", "Northstar Studio", "maya@northstar.studio")]
    [InlineData("Maya Chen", "", "maya@northstar.studio")]
    [InlineData("Maya Chen", "Northstar Studio", "")]
    [InlineData("Maya Chen", "Northstar Studio", "   ")]
    public void Constructor_rejects_blank_required_fields(string name, string company, string email)
    {
        var act = () => new Customer(WorkspaceId, name, company, email);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_replaces_the_editable_fields_without_touching_identity_or_creation_time()
    {
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var originalId = customer.Id;
        var originalCreatedAt = customer.CreatedAt;

        customer.UpdateDetails("Maya C. Chen", "Northstar Studio LLC", "maya@northstarstudio.com", "+1 415 555 0199", "PH");

        customer.Id.Should().Be(originalId);
        customer.CreatedAt.Should().Be(originalCreatedAt);
        customer.Name.Should().Be("Maya C. Chen");
        customer.Company.Should().Be("Northstar Studio LLC");
        customer.Email.Should().Be("maya@northstarstudio.com");
        customer.Phone.Should().Be("+1 415 555 0199");
        customer.Country.Should().Be("PH");
    }
}
