using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Billing;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Billing;

public class RequestSubscriptionUpgradeCommandHandlerTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));

    private static async Task<(SqliteApplicationDbContextFixture Fixture, Guid WorkspaceId)> SeedAsync()
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        return (fixture, workspace.Id);
    }

    [Fact]
    public async Task Handle_creates_a_request_and_returns_a_confirmation()
    {
        var (fixture, workspaceId) = await SeedAsync();
        using var _1 = fixture;
        var handler = new RequestSubscriptionUpgradeCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspaceId), DateTime, new FakeEmailSender(), new FakePublicLinkBuilder(), new FakeBillingSettings());

        var result = await handler.Handle(new RequestSubscriptionUpgradeCommand("Pro", "Monthly", "No GCash account here."), CancellationToken.None);

        result.Tier.Should().Be("Pro");

        var saved = await fixture.CreateContext().SubscriptionUpgradeRequests.SingleAsync(r => r.WorkspaceId == workspaceId);
        saved.Tier.Should().Be(SubscriptionTier.Pro);
        saved.Note.Should().Be("No GCash account here.");
        saved.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_sends_an_admin_notification_when_configured()
    {
        var (fixture, workspaceId) = await SeedAsync();
        using var _1 = fixture;
        var emailSender = new FakeEmailSender();
        var handler = new RequestSubscriptionUpgradeCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspaceId), DateTime, emailSender, new FakePublicLinkBuilder(),
            new FakeBillingSettings { AdminNotificationEmail = "ops@salesdesk.test" });

        await handler.Handle(new RequestSubscriptionUpgradeCommand("Studio", "Annual", null), CancellationToken.None);

        emailSender.SentMessages.Should().ContainSingle();
        emailSender.SentMessages[0].To.Should().Be("ops@salesdesk.test");
        emailSender.SentMessages[0].HtmlBody.Should().Contain("Studio");
    }

    [Fact]
    public async Task Handle_skips_the_notification_when_no_admin_email_is_configured()
    {
        var (fixture, workspaceId) = await SeedAsync();
        using var _1 = fixture;
        var emailSender = new FakeEmailSender();
        var handler = new RequestSubscriptionUpgradeCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspaceId), DateTime, emailSender, new FakePublicLinkBuilder(),
            new FakeBillingSettings { AdminNotificationEmail = null });

        await handler.Handle(new RequestSubscriptionUpgradeCommand("Pro", "Monthly", null), CancellationToken.None);

        emailSender.SentMessages.Should().BeEmpty();
    }
}
