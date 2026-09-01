using FluentAssertions;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Behaviors;
using SalesDesk.Application.Common.Exceptions;

namespace SalesDesk.Application.Tests.Common;

public class EmailVerificationBehaviorTests
{
    private sealed record SampleCommand;

    private sealed record SampleQuery;

    [Fact]
    public async Task Handle_blocks_a_Command_for_an_authenticated_unverified_user()
    {
        var behavior = new EmailVerificationBehavior<SampleCommand, string>(
            new FakeCurrentUserService(Guid.NewGuid()) { IsEmailVerified = false });

        var act = () => behavior.Handle(new SampleCommand(), _ => Task.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_allows_a_Command_for_a_verified_user()
    {
        var behavior = new EmailVerificationBehavior<SampleCommand, string>(
            new FakeCurrentUserService(Guid.NewGuid()) { IsEmailVerified = true });

        var result = await behavior.Handle(new SampleCommand(), _ => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_allows_a_Query_regardless_of_verification_status()
    {
        var behavior = new EmailVerificationBehavior<SampleQuery, string>(
            new FakeCurrentUserService(Guid.NewGuid()) { IsEmailVerified = false });

        var result = await behavior.Handle(new SampleQuery(), _ => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_allows_an_unauthenticated_caller_through_to_the_exempt_auth_Commands()
    {
        var behavior = new EmailVerificationBehavior<ResendVerificationEmailCommand, object?>(
            new FakeCurrentUserService(null, isAuthenticated: false));

        var result = await behavior.Handle(
            new ResendVerificationEmailCommand("maya@northstar.studio"), _ => Task.FromResult<object?>("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_allows_ResendVerificationEmailCommand_even_for_an_authenticated_unverified_caller()
    {
        var behavior = new EmailVerificationBehavior<ResendVerificationEmailCommand, object?>(
            new FakeCurrentUserService(Guid.NewGuid()) { IsEmailVerified = false });

        var result = await behavior.Handle(
            new ResendVerificationEmailCommand("maya@northstar.studio"), _ => Task.FromResult<object?>("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }
}
