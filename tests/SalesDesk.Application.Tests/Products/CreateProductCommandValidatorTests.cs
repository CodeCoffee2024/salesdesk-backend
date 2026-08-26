using FluentAssertions;
using SalesDesk.Application.Products;
using SalesDesk.Domain.Products;

namespace SalesDesk.Application.Tests.Products;

public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    [Fact]
    public void A_valid_command_passes()
    {
        var result = _validator.Validate(new CreateProductCommand("SEO Audit", 750m, ProductUnit.Project, null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_negative_price()
    {
        var result = _validator.Validate(new CreateProductCommand("SEO Audit", -1m, ProductUnit.Project, null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Price");
    }

    [Fact]
    public void Allows_a_zero_price()
    {
        var result = _validator.Validate(new CreateProductCommand("Complimentary consult", 0m, ProductUnit.Hour, null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_blank_name()
    {
        var result = _validator.Validate(new CreateProductCommand("", 750m, ProductUnit.Project, null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }
}
