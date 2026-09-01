using FluentAssertions;
using SalesDesk.Application.Workspaces;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Workspaces;

public class WorkspaceBillingHandlerTests
{
    [Fact]
    public async Task Get_returns_Free_with_no_end_date_for_a_standard_workspace()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetWorkspaceBillingQueryHandler(fixture.CreateContext(), new FakeCurrentUserService(workspace.Id));
        var result = await handler.Handle(new GetWorkspaceBillingQuery(), CancellationToken.None);

        result.SubscriptionTier.Should().Be(nameof(SubscriptionTier.Free));
        result.SubscriptionEndDate.Should().BeNull();
        result.IsEarlyBirdPromo.Should().BeFalse();
    }

    [Fact]
    public async Task Get_returns_Premium_and_the_expiration_date_for_an_early_bird_workspace()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var registeredAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        workspace.GrantEarlyBirdPremium(registeredAt);
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetWorkspaceBillingQueryHandler(fixture.CreateContext(), new FakeCurrentUserService(workspace.Id));
        var result = await handler.Handle(new GetWorkspaceBillingQuery(), CancellationToken.None);

        result.SubscriptionTier.Should().Be(nameof(SubscriptionTier.Premium));
        result.SubscriptionEndDate.Should().Be(registeredAt.AddDays(365));
        result.IsEarlyBirdPromo.Should().BeTrue();
    }
}
