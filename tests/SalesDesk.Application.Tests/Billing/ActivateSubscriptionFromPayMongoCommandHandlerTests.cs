using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Billing;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Billing;

public class ActivateSubscriptionFromPayMongoCommandHandlerTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Handle_activates_the_workspace_and_sends_a_confirmation_email()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "PH", defaultCurrency: "PHP");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var handler = new ActivateSubscriptionFromPayMongoCommandHandler(fixture.CreateContext(), DateTime, emailSender);

        await handler.Handle(new ActivateSubscriptionFromPayMongoCommand(workspace.Id, SubscriptionTier.Pro, "Monthly", "cs_test_123"), CancellationToken.None);

        var updated = await fixture.CreateContext().Workspaces.SingleAsync(w => w.Id == workspace.Id);
        updated.SubscriptionTier.Should().Be(SubscriptionTier.Pro);
        updated.SubscriptionEndDate.Should().Be(DateTime.UtcNow.AddDays(30));
        updated.IsFreeTrial.Should().BeFalse();

        emailSender.SentMessages.Should().ContainSingle();
        emailSender.SentMessages[0].To.Should().Be("hello@northline.studio");
    }

    [Fact]
    public async Task Handle_clears_IsFreeTrial_when_a_trialing_workspace_pays()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "PH", defaultCurrency: "PHP");
        workspace.StartFreeTrial(DateTime.UtcNow);
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new ActivateSubscriptionFromPayMongoCommandHandler(fixture.CreateContext(), DateTime, new FakeEmailSender());
        await handler.Handle(new ActivateSubscriptionFromPayMongoCommand(workspace.Id, SubscriptionTier.Pro, "Annual", "cs_test_456"), CancellationToken.None);

        var updated = await fixture.CreateContext().Workspaces.SingleAsync(w => w.Id == workspace.Id);
        updated.IsFreeTrial.Should().BeFalse();
        updated.SubscriptionEndDate.Should().Be(DateTime.UtcNow.AddDays(365));
    }

    [Fact]
    public async Task Handle_does_not_shorten_an_existing_later_expiry_on_a_duplicate_webhook_delivery()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "PH", defaultCurrency: "PHP");
        workspace.ActivatePaidSubscription(SubscriptionTier.Pro, DateTime.UtcNow.AddDays(365));
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var handler = new ActivateSubscriptionFromPayMongoCommandHandler(fixture.CreateContext(), DateTime, emailSender);

        // A retried/duplicate delivery of the same (or an older) Monthly checkout
        // shouldn't pull an Annual subscriber's expiry back in.
        await handler.Handle(new ActivateSubscriptionFromPayMongoCommand(workspace.Id, SubscriptionTier.Pro, "Monthly", "cs_test_789"), CancellationToken.None);

        var updated = await fixture.CreateContext().Workspaces.SingleAsync(w => w.Id == workspace.Id);
        updated.SubscriptionEndDate.Should().Be(DateTime.UtcNow.AddDays(365));
        emailSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_is_a_no_op_when_the_workspace_no_longer_exists()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new ActivateSubscriptionFromPayMongoCommandHandler(fixture.CreateContext(), DateTime, new FakeEmailSender());

        var act = async () => await handler.Handle(
            new ActivateSubscriptionFromPayMongoCommand(Guid.NewGuid(), SubscriptionTier.Pro, "Monthly", "cs_test_gone"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
