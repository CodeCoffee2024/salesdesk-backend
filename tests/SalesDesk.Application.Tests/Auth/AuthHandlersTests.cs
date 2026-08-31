using FluentAssertions;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Users;
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

    [Fact]
    public async Task Register_creates_a_workspace_and_a_WorkspaceAdmin_user()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());

        var result = await handler.Handle(
            new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        result.User.Role.Should().Be(nameof(Role.WorkspaceAdmin));
        result.Token.Should().NotBeNullOrEmpty();
        fixture.Context.Workspaces.Should().ContainSingle(w => w.Name == "Northstar Studio");
        fixture.Context.Users.Should().ContainSingle(u => u.Email == "maya@northstar.studio" && u.Role == Role.WorkspaceAdmin);
    }

    [Fact]
    public async Task Register_writes_a_WorkspaceRegistered_audit_entry()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var auditLogger = new FakeAuditLogger();
        var handler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, auditLogger);

        var result = await handler.Handle(
            new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        auditLogger.Entries.Should().ContainSingle(e =>
            e.EventType == AuditEventTypes.WorkspaceRegistered && e.WorkspaceId == result.User.WorkspaceId && e.UserId == result.User.Id);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());
        await handler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var act = () => handler.Handle(
            new RegisterCommand("maya@northstar.studio", "another-password", "Someone Else", "Another Studio"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Login_succeeds_with_the_correct_password()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());
        await registerHandler.Handle(new RegisterCommand("maya@northstar.studio", "correct-horse", "Maya Chen", "Northstar Studio"), CancellationToken.None);

        var loginHandler = new LoginCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper);
        var result = await loginHandler.Handle(new LoginCommand("maya@northstar.studio", "correct-horse"), CancellationToken.None);

        result.User.Email.Should().Be("maya@northstar.studio");
    }

    [Fact]
    public async Task Login_rejects_an_incorrect_password()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var registerHandler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());
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
        var registerHandler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());
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
        var registerHandler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());
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
        var registerHandler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());
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
        var registerHandler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());
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
        var registerHandler = new RegisterCommandHandler(fixture.Context, PasswordHasher, TokenService, fixture.Mapper, new FakeAuditLogger());
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
}
