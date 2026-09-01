using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Auth;

/// <summary>
/// TASK-030: backs both the login page's "request a new verification link" option
/// (anonymous) and the persistent in-app banner's "Resend Email" button (already
/// authenticated but unverified) — so it's called both [AllowAnonymous] and by a
/// signed-in caller, and EmailVerificationBehavior exempts it either way.
/// </summary>
public sealed record ResendVerificationEmailCommand(string Email) : IRequest;

public sealed class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
{
    public ResendVerificationEmailCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}

/// <summary>
/// Issues a fresh 24-hour verification token and emails the link — but only if the
/// address belongs to an account that isn't already verified. Either way the
/// handler completes silently and the controller always returns 200, mirroring
/// ForgotPasswordCommandHandler's reasoning: the API must never let this endpoint be
/// used to enumerate registered emails or their verification status.
/// </summary>
public sealed class ResendVerificationEmailCommandHandler(
    IApplicationDbContext context, IEmailSender emailSender, IPublicLinkBuilder linkBuilder, IDateTime dateTime)
    : IRequestHandler<ResendVerificationEmailCommand>
{
    public async Task Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || user.IsEmailVerified)
        {
            return;
        }

        var (rawToken, hash) = SecureTokens.Generate();
        user.IssueEmailVerificationToken(hash, dateTime.UtcNow.UtcDateTime.AddHours(24));
        await context.SaveChangesAsync(cancellationToken);

        var verifyUrl = linkBuilder.BuildVerifyEmailUrl(rawToken);
        await emailSender.SendAsync(
            new EmailMessage(
                user.Email,
                Cc: null,
                Subject: "Verify your SalesDesk email address",
                HtmlBody: $"""
                    <p>Hi {user.FullName},</p>
                    <p>Click the link below to verify your email address. This link expires in 24 hours.</p>
                    <p><a href="{verifyUrl}">Verify your email</a></p>
                    <p>If you didn't request this, you can safely ignore this email.</p>
                    """),
            cancellationToken);
    }
}
