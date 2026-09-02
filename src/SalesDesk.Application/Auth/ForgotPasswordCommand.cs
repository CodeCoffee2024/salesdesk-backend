using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Auth;

public sealed record ForgotPasswordCommand(string Email) : IRequest;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}

/// <summary>
/// Issues a one-hour password-reset token and emails the link, but only if the
/// address belongs to an account — either way the handler completes silently and the
/// controller always returns 200, so the API never reveals which emails are
/// registered (AC from TASK-015, preserved when this stopped being a no-op).
/// </summary>
public sealed class ForgotPasswordCommandHandler(
    IApplicationDbContext context, IEmailSender emailSender, IPublicLinkBuilder linkBuilder, IDateTime dateTime)
    : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return;
        }

        var (rawToken, hash) = SecureTokens.Generate();
        user.IssuePasswordResetToken(hash, dateTime.UtcNow.UtcDateTime.AddHours(1));
        await context.SaveChangesAsync(cancellationToken);

        var resetUrl = linkBuilder.BuildResetPasswordUrl(rawToken);
        var body = $"""
            <p>Hi {user.FullName},</p>
            <p>Click the button below to reset your password. This link expires in 1 hour and can only be used once.</p>
            {EmailBranding.CtaButton("Reset your password", resetUrl)}
            <p>If you didn't request this, you can safely ignore this email.</p>
            """;

        await emailSender.SendAsync(
            new EmailMessage(
                user.Email,
                Cc: null,
                Subject: "Reset your SalesDesk password",
                HtmlBody: EmailBranding.System(body)),
            cancellationToken);
    }
}
