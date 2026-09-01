using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Auth;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<AuthResponseDto>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
        RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(8);
    }
}

/// <summary>
/// Completes the ForgotPasswordCommand flow: exchanges a valid, unexpired reset
/// token for a new password, then signs the user straight in (same response shape
/// as Login/Register) so they don't have to re-enter the new password immediately.
/// </summary>
public sealed class ResetPasswordCommandHandler(
    IApplicationDbContext context, IPasswordHasher passwordHasher, ITokenService tokenService, IMapper mapper, IDateTime dateTime)
    : IRequestHandler<ResetPasswordCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = SecureTokens.Hash(request.Token);
        var user = await context.Users.SingleOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash, cancellationToken);

        if (user is null || !user.TryConsumePasswordResetToken(tokenHash, dateTime.UtcNow.UtcDateTime))
        {
            throw new UnauthorizedAccessException("This reset link is invalid or has expired.");
        }

        user.ChangePasswordHash(passwordHasher.Hash(request.NewPassword));
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
