using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Auth;

public sealed record RegisterCommand(string Email, string Password, string FullName, string WorkspaceName) : IRequest<AuthResponseDto>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Password).NotEmpty().MinimumLength(8);
        RuleFor(c => c.FullName).NotEmpty();
        RuleFor(c => c.WorkspaceName).NotEmpty();
    }
}

/// <summary>
/// Registration provisions two records in one transaction-scoped save: a fresh
/// <see cref="Workspace"/> for the new account, and the registering user as that
/// workspace's <see cref="Role.WorkspaceAdmin"/> — see TASK-015 AC3. There is
/// deliberately no separate "create workspace" flow yet; every account gets exactly
/// one workspace, created here.
///
/// TASK-030: the new user starts with IsEmailVerified = false and a 24-hour
/// verification token issued in the same save, then gets the verification email —
/// EmailVerificationBehavior blocks every other mutation for this account until
/// they follow that link.
///
/// TASK-031: before provisioning the workspace, this also tries to claim one of
/// the first 100 "Early 100 Free Year" promo slots — see
/// IApplicationDbContext.TryReserveEarlyBirdPromoSlotAsync for why that's safe
/// under concurrent registrations. A miss (cap already reached) isn't an error:
/// the workspace is just provisioned as standard Free, same as any other account.
/// </summary>
public sealed class RegisterCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IMapper mapper,
    IAuditLogger auditLogger,
    IEmailSender emailSender,
    IPublicLinkBuilder linkBuilder,
    IDateTime dateTime)
    : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailTaken = await context.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (emailTaken)
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var workspace = new Workspace(request.WorkspaceName, normalizedEmail);

        // TASK-031: reserved before the workspace is even constructed so a
        // "yes, this is the 100th or earlier account" decision is made exactly
        // once, atomically, regardless of anything else this handler does — see
        // TryReserveEarlyBirdPromoSlotAsync's own doc comment.
        if (await context.TryReserveEarlyBirdPromoSlotAsync(cancellationToken))
        {
            workspace.GrantEarlyBirdPremium(dateTime.UtcNow);
        }

        context.Workspaces.Add(workspace);

        var user = new User(normalizedEmail, passwordHasher.Hash(request.Password), request.FullName, Role.WorkspaceAdmin, workspace.Id);

        var (rawToken, hash) = SecureTokens.Generate();
        user.IssueEmailVerificationToken(hash, dateTime.UtcNow.UtcDateTime.AddHours(24));

        context.Users.Add(user);

        await context.SaveChangesAsync(cancellationToken);

        // TASK-017 AC4: tenant registration is a critical platform event.
        await auditLogger.LogAsync(
            AuditEventTypes.WorkspaceRegistered,
            $"Workspace \"{workspace.Name}\" registered by {user.Email}.",
            workspace.Id,
            user.Id,
            cancellationToken);

        var verifyUrl = linkBuilder.BuildVerifyEmailUrl(rawToken);
        await emailSender.SendAsync(
            new EmailMessage(
                user.Email,
                Cc: null,
                Subject: "Verify your SalesDesk email address",
                HtmlBody: $"""
                    <p>Hi {user.FullName},</p>
                    <p>Welcome to SalesDesk! Click the link below to verify your email address. This link expires in 24 hours.</p>
                    <p><a href="{verifyUrl}">Verify your email</a></p>
                    """),
            cancellationToken);

        var accessToken = tokenService.IssueToken(user);

        return new AuthResponseDto
        {
            Token = accessToken.Value,
            ExpiresAt = accessToken.ExpiresAt,
            User = mapper.Map<UserDto>(user)
        };
    }
}
