using FluentAssertions;
using SalesDesk.Application.Documents;

namespace SalesDesk.Application.Tests.Documents;

public class CreateDocumentLineItemRequestValidatorTests
{
    private readonly CreateDocumentLineItemRequestValidator _validator = new();

    [Fact]
    public void A_valid_line_item_passes()
    {
        var result = _validator.Validate(new CreateDocumentLineItemRequest("Research", 2m, 500m, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_an_empty_description()
    {
        var result = _validator.Validate(new CreateDocumentLineItemRequest("", 1m, 500m, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_non_positive_quantity(decimal quantity)
    {
        var result = _validator.Validate(new CreateDocumentLineItemRequest("Research", quantity, 500m, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }

    [Fact]
    public void Rejects_a_negative_unit_price()
    {
        var result = _validator.Validate(new CreateDocumentLineItemRequest("Research", 1m, -1m, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UnitPrice");
    }

    [Fact]
    public void Allows_a_zero_unit_price()
    {
        var result = _validator.Validate(new CreateDocumentLineItemRequest("Complimentary consult", 1m, 0m, null));

        result.IsValid.Should().BeTrue();
    }
}
