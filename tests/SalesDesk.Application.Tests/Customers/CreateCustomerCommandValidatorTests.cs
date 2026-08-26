using FluentAssertions;
using SalesDesk.Application.Customers;

namespace SalesDesk.Application.Tests.Customers;

public class CreateCustomerCommandValidatorTests
{
    private readonly CreateCustomerCommandValidator _validator = new();

    [Fact]
    public void A_valid_command_passes()
    {
        var result = _validator.Validate(new CreateCustomerCommand("Maya Chen", "Northstar Studio", "maya@northstar.studio", null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("maya@")]
    [InlineData("@northstar.studio")]
    [InlineData("maya northstar.studio")]
    public void Rejects_malformed_email_addresses(string email)
    {
        var result = _validator.Validate(new CreateCustomerCommand("Maya Chen", "Northstar Studio", email, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Rejects_a_blank_name()
    {
        var result = _validator.Validate(new CreateCustomerCommand("", "Northstar Studio", "maya@northstar.studio", null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Rejects_a_blank_company()
    {
        var result = _validator.Validate(new CreateCustomerCommand("Maya Chen", "", "maya@northstar.studio", null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Company");
    }
}
