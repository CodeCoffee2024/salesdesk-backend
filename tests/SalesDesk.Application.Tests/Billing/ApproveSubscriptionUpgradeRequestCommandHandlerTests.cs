using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Billing;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Billing;

public class ApproveSubscriptionUpgradeRequestCommandHandlerTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));

    // Mirrors ApproveGCashPaymentCommandHandlerTests.ExtractToken — the raw
    // approval token is never persisted (only its hash is), so the only way a
    // test can get a working token is to read it out of the notification email.
    private static string ExtractToken(string htmlBody)
    {
        const string marker = "approve-upgrade-request?token=";
        var start = htmlBody.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = htmlBody.IndexOf('"', start);
        return htmlBody[start..end];
    }

    private static async Task<(SqliteApplicationDbContextFixture Fixture, Guid WorkspaceId, string RawToken)> SeedRequestedAsync(
        string tier = "Pro", string billingCycle = "Monthly")
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var requestEmailSender = new FakeEmailSender();
        var requestHandler = new RequestSubscriptionUpgradeCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspace.Id), DateTime, requestEmailSender, new FakePublicLinkBuilder(),
            new FakeBillingSettings { AdminNotificationEmail = "ops@salesdesk.test" });

        await requestHandler.Handle(new RequestSubscriptionUpgradeCommand(tier, billingCycle, null), CancellationToken.None);

        var rawToken = ExtractToken(requestEmailSender.SentMessages[0].HtmlBody);
        return (fixture, workspace.Id, rawToken);
    }

    [Fact]
    public async Task Handle_activates_the_workspace_and_sends_a_confirmation_email()
    {
        var (fixture, workspaceId, rawToken) = await SeedRequestedAsync(tier: "Studio", billingCycle: "Annual");
        using var _1 = fixture;
        var emailSender = new FakeEmailSender();
        var handler = new ApproveSubscriptionUpgradeRequestCommandHandler(fixture.CreateContext(), DateTime, emailSender);

        var result = await handler.Handle(new ApproveSubscriptionUpgradeRequestCommand(rawToken), CancellationToken.None);

        result.WasAlreadyApproved.Should().BeFalse();
        result.Tier.Should().Be("Studio");
        result.ExpiresAtUtc.Should().Be(DateTime.UtcNow.AddDays(365));

        var workspace = await fixture.CreateContext().Workspaces.SingleAsync(w => w.Id == workspaceId);
        workspace.SubscriptionTier.Should().Be(SubscriptionTier.Studio);

        emailSender.SentMessages.Should().ContainSingle();
        emailSender.SentMessages[0].To.Should().Be("hello@northline.studio");
    }

    [Fact]
    public async Task Handle_is_idempotent_on_a_second_approval_and_sends_no_second_email()
    {
        var (fixture, _, rawToken) = await SeedRequestedAsync();
        using var _1 = fixture;
        var firstHandler = new ApproveSubscriptionUpgradeRequestCommandHandler(fixture.CreateContext(), DateTime, new FakeEmailSender());
        await firstHandler.Handle(new ApproveSubscriptionUpgradeRequestCommand(rawToken), CancellationToken.None);

        var secondEmailSender = new FakeEmailSender();
        var secondHandler = new ApproveSubscriptionUpgradeRequestCommandHandler(fixture.CreateContext(), DateTime, secondEmailSender);
        var result = await secondHandler.Handle(new ApproveSubscriptionUpgradeRequestCommand(rawToken), CancellationToken.None);

        result.WasAlreadyApproved.Should().BeTrue();
        secondEmailSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_token()
    {
        var (fixture, _, _) = await SeedRequestedAsync();
        using var _1 = fixture;
        var handler = new ApproveSubscriptionUpgradeRequestCommandHandler(fixture.CreateContext(), DateTime, new FakeEmailSender());

        var act = () => handler.Handle(new ApproveSubscriptionUpgradeRequestCommand("not-the-right-token"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
