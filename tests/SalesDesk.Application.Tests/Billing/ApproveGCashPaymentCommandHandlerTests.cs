using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Billing;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Billing;

public class ApproveGCashPaymentCommandHandlerTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));

    // Mirrors AuthHandlersTests.ExtractToken — the raw approval token is never
    // persisted (only its hash is, on GCashPaymentSubmission.ApprovalTokenHash),
    // so the only way a test can get a working token is to read it the same way
    // the admin would: out of the notification email's approve link.
    private static string ExtractToken(string htmlBody)
    {
        const string marker = "approve?token=";
        var start = htmlBody.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = htmlBody.IndexOf('"', start);
        return htmlBody[start..end];
    }

    private static async Task<(SqliteApplicationDbContextFixture Fixture, Guid WorkspaceId, string RawToken)> SeedSubmittedAsync(
        string tier = "Pro", string billingCycle = "Monthly")
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "PH", defaultCurrency: "PHP");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var submitEmailSender = new FakeEmailSender();
        var submitHandler = new SubmitGCashPaymentCommandHandler(
            fixture.CreateContext(), new FakeCurrentUserService(workspace.Id), DateTime, submitEmailSender, new FakePublicLinkBuilder(),
            new FakeBillingSettings { AdminNotificationEmail = "ops@salesdesk.test" });

        await submitHandler.Handle(
            new SubmitGCashPaymentCommand(tier, billingCycle, "1234567890123", "Maya Chen", "09171234567", null),
            CancellationToken.None);

        var rawToken = ExtractToken(submitEmailSender.SentMessages[0].HtmlBody);
        return (fixture, workspace.Id, rawToken);
    }

    [Fact]
    public async Task Handle_activates_the_workspace_and_sends_a_confirmation_email()
    {
        var (fixture, workspaceId, rawToken) = await SeedSubmittedAsync(tier: "Studio", billingCycle: "Annual");
        using var _1 = fixture;
        var emailSender = new FakeEmailSender();
        var handler = new ApproveGCashPaymentCommandHandler(fixture.CreateContext(), DateTime, emailSender);

        var result = await handler.Handle(new ApproveGCashPaymentCommand(rawToken), CancellationToken.None);

        result.WasAlreadyApproved.Should().BeFalse();
        result.Tier.Should().Be("Studio");
        result.ExpiresAtUtc.Should().Be(DateTime.UtcNow.AddDays(365));

        var workspace = await fixture.CreateContext().Workspaces.SingleAsync(w => w.Id == workspaceId);
        workspace.SubscriptionTier.Should().Be(SubscriptionTier.Studio);
        workspace.SubscriptionEndDate.Should().Be(DateTime.UtcNow.AddDays(365));

        emailSender.SentMessages.Should().ContainSingle();
        emailSender.SentMessages[0].To.Should().Be("hello@northline.studio");
    }

    [Fact]
    public async Task Handle_is_idempotent_on_a_second_approval_and_sends_no_second_email()
    {
        var (fixture, _, rawToken) = await SeedSubmittedAsync();
        using var _1 = fixture;
        var firstHandler = new ApproveGCashPaymentCommandHandler(fixture.CreateContext(), DateTime, new FakeEmailSender());
        await firstHandler.Handle(new ApproveGCashPaymentCommand(rawToken), CancellationToken.None);

        var secondEmailSender = new FakeEmailSender();
        var secondHandler = new ApproveGCashPaymentCommandHandler(fixture.CreateContext(), DateTime, secondEmailSender);
        var result = await secondHandler.Handle(new ApproveGCashPaymentCommand(rawToken), CancellationToken.None);

        result.WasAlreadyApproved.Should().BeTrue();
        secondEmailSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_token()
    {
        var (fixture, _, _) = await SeedSubmittedAsync();
        using var _1 = fixture;
        var handler = new ApproveGCashPaymentCommandHandler(fixture.CreateContext(), DateTime, new FakeEmailSender());

        var act = () => handler.Handle(new ApproveGCashPaymentCommand("not-the-right-token"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
