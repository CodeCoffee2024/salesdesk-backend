using FluentAssertions;
using SalesDesk.Application.Auth;

namespace SalesDesk.Application.Tests.Auth;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void A_valid_command_passes()
    {
        var result = _validator.Validate(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_malformed_email()
    {
        var result = _validator.Validate(new RegisterCommand("not-an-email", "correct-horse", "Maya Chen", "Northstar Studio"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Rejects_a_password_shorter_than_8_characters()
    {
        var result = _validator.Validate(new RegisterCommand("maya@northstar.studio", "short", "Maya Chen", "Northstar Studio"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void Rejects_a_blank_workspace_name()
    {
        var result = _validator.Validate(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "WorkspaceName");
    }
}
