using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Auth;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher, ITokenService tokenService, IMapper mapper)
    : IRequestHandler<LoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // Same message whether the email is unknown or the password is wrong —
        // distinguishing the two would let a caller enumerate registered accounts.
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // TASK-017: a SystemAdmin can suspend a tenant from the admin console —
        // credentials can be valid while the workspace itself is locked out.
        var workspace = await context.Workspaces.FindAsync([user.WorkspaceId], cancellationToken);
        if (workspace is null || !workspace.IsActive)
        {
            throw new ForbiddenException("This workspace has been suspended. Contact support for assistance.");
        }

        var accessToken = tokenService.IssueToken(user);

        return new AuthResponseDto
        {
            Token = accessToken.Value,
            ExpiresAt = accessToken.ExpiresAt,
            User = mapper.Map<UserDto>(user)
        };
    }
}
