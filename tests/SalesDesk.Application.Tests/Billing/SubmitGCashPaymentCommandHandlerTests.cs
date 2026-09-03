using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Billing;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Billing;

public class SubmitGCashPaymentCommandHandlerTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));

    private static async Task<(SqliteApplicationDbContextFixture Fixture, Guid WorkspaceId)> SeedAsync(string country = "PH")
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: country, defaultCurrency: country == "PH" ? "PHP" : "USD");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        return (fixture, workspace.Id);
    }

    private static SubmitGCashPaymentCommand ValidCommand(string? screenshotDataUrl = null) =>
        new("Pro", "Monthly", "1234567890123", "Maya Chen", "09171234567", screenshotDataUrl);

    [Fact]
    public async Task Handle_creates_a_submission_and_returns_the_reference_number()
    {
        var (fixture, workspaceId) = await SeedAsync();
        using var _1 = fixture;
        var handler = new SubmitGCashPaymentCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspaceId), DateTime, new FakeEmailSender(), new FakePublicLinkBuilder(), new FakeBillingSettings());

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.GCashReferenceNumber.Should().Be("1234567890123");

        var saved = await fixture.CreateContext().GCashPaymentSubmissions.SingleAsync(s => s.WorkspaceId == workspaceId);
        saved.Tier.Should().Be(SubscriptionTier.Pro);
        saved.AmountPhp.Should().Be(199m);
        saved.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_sends_an_admin_notification_when_configured()
    {
        var (fixture, workspaceId) = await SeedAsync();
        using var _1 = fixture;
        var emailSender = new FakeEmailSender();
        var handler = new SubmitGCashPaymentCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspaceId), DateTime, emailSender, new FakePublicLinkBuilder(),
            new FakeBillingSettings { AdminNotificationEmail = "ops@salesdesk.test" });

        await handler.Handle(ValidCommand(), CancellationToken.None);

        emailSender.SentMessages.Should().ContainSingle();
        emailSender.SentMessages[0].To.Should().Be("ops@salesdesk.test");
        emailSender.SentMessages[0].HtmlBody.Should().Contain("1234567890123");
    }

    [Fact]
    public async Task Handle_skips_the_notification_when_no_admin_email_is_configured()
    {
        var (fixture, workspaceId) = await SeedAsync();
        using var _1 = fixture;
        var emailSender = new FakeEmailSender();
        var handler = new SubmitGCashPaymentCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspaceId), DateTime, emailSender, new FakePublicLinkBuilder(),
            new FakeBillingSettings { AdminNotificationEmail = null });

        await handler.Handle(ValidCommand(), CancellationToken.None);

        emailSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_embeds_the_screenshot_in_the_admin_email_when_provided()
    {
        var (fixture, workspaceId) = await SeedAsync();
        using var _1 = fixture;
        var emailSender = new FakeEmailSender();
        var handler = new SubmitGCashPaymentCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspaceId), DateTime, emailSender, new FakePublicLinkBuilder(),
            new FakeBillingSettings { AdminNotificationEmail = "ops@salesdesk.test" });
        const string dataUrl = "data:image/png;base64,abc==";

        await handler.Handle(ValidCommand(dataUrl), CancellationToken.None);

        emailSender.SentMessages[0].HtmlBody.Should().Contain(dataUrl);
    }

    [Fact]
    public async Task Handle_rejects_a_workspace_that_is_not_in_the_Philippines()
    {
        var (fixture, workspaceId) = await SeedAsync(country: "US");
        using var _1 = fixture;
        var handler = new SubmitGCashPaymentCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspaceId), DateTime, new FakeEmailSender(), new FakePublicLinkBuilder(), new FakeBillingSettings());

        var act = () => handler.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
