using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Promotions;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;
using SalesDesk.Infrastructure.Services;

namespace SalesDesk.Application.Tests.Auth;

/// <summary>Fixed, non-expiring stub — these tests only care that a handler asked for a token, not JWT specifics.</summary>
internal sealed class FakeTokenService : ITokenService
{
    public AccessToken IssueToken(User user) => new($"fake-token-for-{user.Id}", DateTimeOffset.UtcNow.AddHours(1));

    public AccessToken IssueImpersonationToken(User target, TimeSpan lifetime) =>
        new($"fake-impersonation-token-for-{target.Id}", DateTimeOffset.UtcNow.Add(lifetime));
}

public class AuthHandlersTests
{
    private static readonly PasswordHasher PasswordHasher = new();
    private static readonly FakeTokenService TokenService = new();

    /// <summary>
    /// TASK-030 added three constructor params to RegisterCommandHandler (email
    /// verification's sender/link-builder/clock) that most of these tests don't
    /// otherwise care about — this keeps the many call sites that only need a
    /// working registration, not an inspectable verification email, from each
    /// having to spell out fresh fakes for all three.
    /// </summary>
    private static RegisterCommandHandler CreateRegisterHandler(
        SqliteApplicationDbContextFixture fixture,
        IApplicationDbContext? context = null,
        IAuditLogger? auditLogger = null,
        IEmailSender? emailSender = null,
        IPublicLinkBuilder? linkBuilder = null,
        IDateTime? dateTime = null) =>
        new(
            context ?? fixture.Context,
            PasswordHasher,
            TokenService,
            fixture.Mapper,
            auditLogger ?? new FakeAuditLogger(),
            emailSender ?? new FakeEmailSender(),
            linkBuilder ?? new FakePublicLinkBuilder(),
            dateTime ?? new FakeDateTime(DateTimeOffset.UtcNow));

    [Fact]
    public async Task Register_creates_a_workspace_and_a_WorkspaceAdmin_user()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = CreateRegisterHandler(fixture);

        var result = await handler.Handle(
            new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        result.User.Role.Should().Be(nameof(Role.WorkspaceAdmin));
        result.User.IsEmailVerified.Should().BeFalse();
        result.Token.Should().NotBeNullOrEmpty();
        fixture.Context.Workspaces.Should().ContainSingle(w => w.Name == "Northstar Studio");
        fixture.Context.Users.Should().ContainSingle(u => u.Email == "maya@northstar.studio" && u.Role == Role.WorkspaceAdmin);
    }

    [Fact]
    public async Task Register_writes_a_WorkspaceRegistered_audit_entry()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var auditLogger = new FakeAuditLogger();
        var handler = CreateRegisterHandler(fixture, auditLogger: auditLogger);

        var result = await handler.Handle(
            new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        auditLogger.Entries.Should().ContainSingle(e =>
            e.EventType == AuditEventTypes.WorkspaceRegistered && e.WorkspaceId == result.User.WorkspaceId && e.UserId == result.User.Id);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = CreateRegisterHandler(fixture);
        await handler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var act = () => handler.Handle(
            new RegisterCommand("maya@northstar.studio", "another-password", "Someone Else", "Another Studio"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Login_succeeds_with_the_correct_password()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var loginHandler = new LoginCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper);
        var result = await loginHandler.Handle(new LoginCommand("maya@northstar.studio", "correct-horse"), CancellationToken.None);

        result.User.Email.Should().Be("maya@northstar.studio");
    }

    [Fact]
    public async Task Login_rejects_an_incorrect_password()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var loginHandler = new LoginCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper);
        var act = () => loginHandler.Handle(new LoginCommand("maya@northstar.studio", "wrong-password"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Login_rejects_an_unknown_email()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var loginHandler = new LoginCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper);

        var act = () => loginHandler.Handle(new LoginCommand("nobody@northstar.studio", "whatever"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Login_rejects_a_user_whose_workspace_has_been_suspended()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var workspace = fixture.Context.Workspaces.Single(w => w.Name == "Northstar Studio");
        workspace.Suspend();
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var loginHandler = new LoginCommandHandler(fixture.CreateContext(), PasswordHasher, TokenService, fixture.Mapper);
        var act = () => loginHandler.Handle(new LoginCommand("maya@northstar.studio", "correct-horse"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ForgotPassword_emails_a_reset_link_for_a_known_address()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var handler = new ForgotPasswordCommandHandler(fixture.CreateContext(), emailSender, new FakePublicLinkBuilder(), new FakeDateTime(DateTimeOffset.UtcNow));

        await handler.Handle(new ForgotPasswordCommand("maya@northstar.studio"), CancellationToken.None);

        emailSender.SentMessages.Should().ContainSingle(m => m.To == "maya@northstar.studio" && m.HtmlBody.Contains("reset-password?token="));
    }

    [Fact]
    public async Task ForgotPassword_sends_nothing_for_an_unknown_address()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var emailSender = new FakeEmailSender();
        var handler = new ForgotPasswordCommandHandler(fixture.Context, emailSender, new FakePublicLinkBuilder(), new FakeDateTime(DateTimeOffset.UtcNow));

        await handler.Handle(new ForgotPasswordCommand("nobody@northstar.studio"), CancellationToken.None);

        emailSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetPassword_signs_in_with_a_new_password_given_a_valid_token()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var now = new FakeDateTime(DateTimeOffset.UtcNow);
        var forgotHandler = new ForgotPasswordCommandHandler(fixture.CreateContext(), emailSender, new FakePublicLinkBuilder(), now);
        await forgotHandler.Handle(new ForgotPasswordCommand("maya@northstar.studio"), CancellationToken.None);

        var rawToken = ExtractToken(emailSender.SentMessages[0].HtmlBody);
        var resetHandler = new ResetPasswordCommandHandler(fixture.CreateContext(), PasswordHasher, TokenService, fixture.Mapper, now);
        var result = await resetHandler.Handle(new ResetPasswordCommand(rawToken, "new-correct-horse"), CancellationToken.None);

        result.User.Email.Should().Be("maya@northstar.studio");

        var loginHandler = new LoginCommandHandler(fixture.CreateContext(), PasswordHasher, TokenService, fixture.Mapper);
        var loginResult = await loginHandler.Handle(new LoginCommand("maya@northstar.studio", "new-correct-horse"), CancellationToken.None);
        loginResult.User.Email.Should().Be("maya@northstar.studio");
    }

    [Fact]
    public async Task ResetPassword_rejects_an_unknown_token()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var resetHandler = new ResetPasswordCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeDateTime(DateTimeOffset.UtcNow));

        var act = () => resetHandler.Handle(new ResetPasswordCommand("not-a-real-token", "new-password"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ResetPassword_rejects_an_expired_token()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var issuedAt = new FakeDateTime(DateTimeOffset.UtcNow);
        var forgotHandler = new ForgotPasswordCommandHandler(fixture.CreateContext(), emailSender, new FakePublicLinkBuilder(), issuedAt);
        await forgotHandler.Handle(new ForgotPasswordCommand("maya@northstar.studio"), CancellationToken.None);

        var rawToken = ExtractToken(emailSender.SentMessages[0].HtmlBody);
        var twoHoursLater = new FakeDateTime(issuedAt.UtcNow.AddHours(2));
        var resetHandler = new ResetPasswordCommandHandler(fixture.CreateContext(), PasswordHasher, TokenService, fixture.Mapper, twoHoursLater);

        var act = () => resetHandler.Handle(new ResetPasswordCommand(rawToken, "new-password"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ResetPassword_rejects_a_token_that_was_already_used()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var now = new FakeDateTime(DateTimeOffset.UtcNow);
        var forgotHandler = new ForgotPasswordCommandHandler(fixture.CreateContext(), emailSender, new FakePublicLinkBuilder(), now);
        await forgotHandler.Handle(new ForgotPasswordCommand("maya@northstar.studio"), CancellationToken.None);

        var rawToken = ExtractToken(emailSender.SentMessages[0].HtmlBody);
        var firstUse = new ResetPasswordCommandHandler(fixture.CreateContext(), PasswordHasher, TokenService, fixture.Mapper, now);
        await firstUse.Handle(new ResetPasswordCommand(rawToken, "new-correct-horse"), CancellationToken.None);

        var secondUse = new ResetPasswordCommandHandler(fixture.CreateContext(), PasswordHasher, TokenService, fixture.Mapper, now);
        var act = () => secondUse.Handle(new ResetPasswordCommand(rawToken, "another-password"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static string ExtractToken(string htmlBody)
    {
        const string marker = "reset-password?token=";
        var start = htmlBody.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = htmlBody.IndexOf('"', start);
        return htmlBody[start..end];
    }

    [Fact]
    public async Task CompleteOnboarding_marks_the_current_user_as_onboarded()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        var registerResult = await registerHandler.Handle(
            new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        registerResult.User.HasCompletedOnboarding.Should().BeFalse();

        var currentUser = new FakeCurrentUserService(registerResult.User.WorkspaceId, registerResult.User.Id);
        var handler = new CompleteOnboardingCommandHandler(fixture.CreateContext(), currentUser);
        await handler.Handle(new CompleteOnboardingCommand(), CancellationToken.None);

        // Re-queried through a brand new context, not the one RegisterCommandHandler
        // wrote through, so this actually proves the change persisted rather than
        // just reflecting a stale in-memory tracked instance.
        var user = fixture.CreateContext().Users.Single(u => u.Id == registerResult.User.Id);
        user.HasCompletedOnboarding.Should().BeTrue();
    }

    [Fact]
    public async Task Register_sends_a_verification_email_and_starts_unverified()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var emailSender = new FakeEmailSender();
        var handler = CreateRegisterHandler(fixture, emailSender: emailSender);

        var result = await handler.Handle(
            new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        result.User.IsEmailVerified.Should().BeFalse();
        emailSender.SentMessages.Should().ContainSingle(m => m.To == "maya@northstar.studio" && m.HtmlBody.Contains("auth/verify-email?token="));
    }

    [Fact]
    public async Task VerifyEmail_marks_the_account_verified_given_a_valid_token()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var emailSender = new FakeEmailSender();
        var registerHandler = CreateRegisterHandler(fixture, emailSender: emailSender);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var rawToken = ExtractVerificationToken(emailSender.SentMessages[0].HtmlBody);
        var verifyHandler = new VerifyEmailCommandHandler(fixture.CreateContext(), TokenService, fixture.Mapper, new FakeDateTime(DateTimeOffset.UtcNow));
        var result = await verifyHandler.Handle(new VerifyEmailCommand(rawToken), CancellationToken.None);

        result.User.IsEmailVerified.Should().BeTrue();

        var user = fixture.CreateContext().Users.Single(u => u.Email == "maya@northstar.studio");
        user.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmail_rejects_an_unknown_token()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var verifyHandler = new VerifyEmailCommandHandler(fixture.Context, TokenService, fixture.Mapper, new FakeDateTime(DateTimeOffset.UtcNow));

        var act = () => verifyHandler.Handle(new VerifyEmailCommand("not-a-real-token"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task VerifyEmail_rejects_an_expired_token()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var emailSender = new FakeEmailSender();
        var issuedAt = new FakeDateTime(DateTimeOffset.UtcNow);
        var registerHandler = CreateRegisterHandler(fixture, emailSender: emailSender, dateTime: issuedAt);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var rawToken = ExtractVerificationToken(emailSender.SentMessages[0].HtmlBody);
        var twentyFiveHoursLater = new FakeDateTime(issuedAt.UtcNow.AddHours(25));
        var verifyHandler = new VerifyEmailCommandHandler(fixture.CreateContext(), TokenService, fixture.Mapper, twentyFiveHoursLater);

        var act = () => verifyHandler.Handle(new VerifyEmailCommand(rawToken), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ResendVerificationEmail_sends_a_new_link_for_an_unverified_account()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = CreateRegisterHandler(fixture);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var resendHandler = new ResendVerificationEmailCommandHandler(fixture.CreateContext(), emailSender, new FakePublicLinkBuilder(), new FakeDateTime(DateTimeOffset.UtcNow));

        await resendHandler.Handle(new ResendVerificationEmailCommand("maya@northstar.studio"), CancellationToken.None);

        emailSender.SentMessages.Should().ContainSingle(m => m.To == "maya@northstar.studio" && m.HtmlBody.Contains("auth/verify-email?token="));
    }

    [Fact]
    public async Task ResendVerificationEmail_sends_nothing_for_an_already_verified_account()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registrationEmailSender = new FakeEmailSender();
        var registerHandler = CreateRegisterHandler(fixture, emailSender: registrationEmailSender);
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var rawToken = ExtractVerificationToken(registrationEmailSender.SentMessages[0].HtmlBody);
        var verifyHandler = new VerifyEmailCommandHandler(fixture.CreateContext(), TokenService, fixture.Mapper, new FakeDateTime(DateTimeOffset.UtcNow));
        await verifyHandler.Handle(new VerifyEmailCommand(rawToken), CancellationToken.None);

        var resendEmailSender = new FakeEmailSender();
        var resendHandler = new ResendVerificationEmailCommandHandler(fixture.CreateContext(), resendEmailSender, new FakePublicLinkBuilder(), new FakeDateTime(DateTimeOffset.UtcNow));
        await resendHandler.Handle(new ResendVerificationEmailCommand("maya@northstar.studio"), CancellationToken.None);

        resendEmailSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationEmail_sends_nothing_for_an_unknown_address()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var emailSender = new FakeEmailSender();
        var resendHandler = new ResendVerificationEmailCommandHandler(fixture.Context, emailSender, new FakePublicLinkBuilder(), new FakeDateTime(DateTimeOffset.UtcNow));

        await resendHandler.Handle(new ResendVerificationEmailCommand("nobody@northstar.studio"), CancellationToken.None);

        emailSender.SentMessages.Should().BeEmpty();
    }

    private static string ExtractVerificationToken(string htmlBody)
    {
        const string marker = "auth/verify-email?token=";
        var start = htmlBody.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = htmlBody.IndexOf('"', start);
        return htmlBody[start..end];
    }

    [Fact]
    public async Task Register_grants_the_early_bird_promo_to_an_early_registration()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = CreateRegisterHandler(fixture);

        var result = await handler.Handle(
            new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var workspace = fixture.CreateContext().Workspaces.Single(w => w.Id == result.User.WorkspaceId);
        workspace.SubscriptionTier.Should().Be(SubscriptionTier.Pro);
        workspace.IsEarlyBirdPromo.Should().BeTrue();
        workspace.SubscriptionEndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_falls_back_to_standard_Free_provisioning_once_the_promo_cap_is_reached()
    {
        using var fixture = new SqliteApplicationDbContextFixture();

        // Fast-forward straight to "the cap is already reached" rather than
        // registering 100 accounts — the boundary itself (the 100th vs. the
        // 101st) is covered by EarlyBirdPromoReservationTests; this test only
        // needs to prove that registration #101 onward doesn't crash and simply
        // provisions Free, per TASK-031's graceful-fallback AC.
        await fixture.CreateContext().Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE promo_counters SET count = {PromoCounter.EarlyBirdCap} WHERE key = {PromoCounter.EarlyBirdPremiumKey}",
            CancellationToken.None);

        var handler = CreateRegisterHandler(fixture);
        var result = await handler.Handle(
            new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var workspace = fixture.CreateContext().Workspaces.Single(w => w.Id == result.User.WorkspaceId);
        workspace.SubscriptionTier.Should().Be(SubscriptionTier.Free);
        workspace.IsEarlyBirdPromo.Should().BeFalse();
        workspace.SubscriptionEndDate.Should().BeNull();
    }
}
