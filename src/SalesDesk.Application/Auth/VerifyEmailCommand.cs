using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Auth;

public sealed record VerifyEmailCommand(string Token) : IRequest<AuthResponseDto>;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
    }
}

/// <summary>
/// TASK-030: completes the registration flow's verification-email link. Exchanges a
/// valid, unexpired verification token for IsEmailVerified = true, then signs the
/// user straight in with a fresh token — same shape as Login/Register/ResetPassword
/// — so the frontend's /auth/verify-email page can immediately replace whatever
/// (unverified) token it was already holding with one that carries the updated
/// email_verified claim, without forcing a re-login.
/// </summary>
public sealed class VerifyEmailCommandHandler(
    IApplicationDbContext context, ITokenService tokenService, IMapper mapper, IDateTime dateTime)
    : IRequestHandler<VerifyEmailCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = SecureTokens.Hash(request.Token);
        var user = await context.Users.SingleOrDefaultAsync(u => u.EmailVerificationTokenHash == tokenHash, cancellationToken);

        if (user is null || !user.TryConsumeEmailVerificationToken(tokenHash, dateTime.UtcNow.UtcDateTime))
        {
            throw new UnauthorizedAccessException("This verification link is invalid or has expired.");
        }

        await context.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.IssueToken(user);

        return new AuthResponseDto
        {
            Token = accessToken.Value,
            ExpiresAt = accessToken.ExpiresAt,
            User = mapper.Map<UserDto>(user)
        };
    }
}
