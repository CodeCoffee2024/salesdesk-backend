using FluentAssertions;
using FluentValidation;
using SalesDesk.Application.Common.Behaviors;

namespace SalesDesk.Application.Tests.Common;

public class ValidationBehaviorTests
{
    private sealed record SampleRequest(string Name);

    [Fact]
    public async Task Handle_calls_next_when_there_are_no_validators()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([]);
        var nextCalled = false;

        var result = await behavior.Handle(new SampleRequest("anything"), Next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.Should().Be("ok");

        Task<string> Next(CancellationToken _)
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }
    }

    [Fact]
    public async Task Handle_calls_next_when_every_validator_passes()
    {
        var passingValidator = new InlineValidator<SampleRequest>();
        passingValidator.RuleFor(r => r.Name).NotEmpty();
        var behavior = new ValidationBehavior<SampleRequest, string>([passingValidator]);

        var result = await behavior.Handle(new SampleRequest("Maya"), _ => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_throws_ValidationException_with_the_failures_when_a_validator_fails()
    {
        var failingValidator = new InlineValidator<SampleRequest>();
        failingValidator.RuleFor(r => r.Name).NotEmpty().WithMessage("Name is required.");
        var behavior = new ValidationBehavior<SampleRequest, string>([failingValidator]);

        var act = () => behavior.Handle(new SampleRequest(""), _ => Task.FromResult("ok"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle(e => e.ErrorMessage == "Name is required.");
    }

    [Fact]
    public async Task Handle_aggregates_failures_from_multiple_validators()
    {
        var validatorOne = new InlineValidator<SampleRequest>();
        validatorOne.RuleFor(r => r.Name).NotEmpty().WithMessage("Name is required.");
        var validatorTwo = new InlineValidator<SampleRequest>();
        validatorTwo.RuleFor(r => r.Name).MinimumLength(10).WithMessage("Name is too short.");

        var behavior = new ValidationBehavior<SampleRequest, string>([validatorOne, validatorTwo]);

        var act = () => behavior.Handle(new SampleRequest(""), _ => Task.FromResult("ok"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Select(e => e.ErrorMessage).Should().Contain(["Name is required.", "Name is too short."]);
    }
}
