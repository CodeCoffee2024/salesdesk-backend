using AutoMapper;
using MediatR;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Users;

namespace SalesDesk.Application.Admin;

/// <summary>
/// Lets a SystemAdmin briefly see the app exactly as another user would, for
/// support/debugging — the "view as" feature of the admin console's Users
/// directory. Returns the same <see cref="AuthResponseDto"/> shape login/register
/// do, built from a short-lived token for the target user.
/// </summary>
public sealed record ImpersonateUserCommand(Guid UserId) : IRequest<AuthResponseDto>;

public sealed class ImpersonateUserCommandHandler(
    IApplicationDbContext context, ITokenService tokenService, IMapper mapper, IAuditLogger auditLogger, ICurrentUserService currentUser)
    : IRequestHandler<ImpersonateUserCommand, AuthResponseDto>
{
    private static readonly TimeSpan ImpersonationLifetime = TimeSpan.FromMinutes(30);

    public async Task<AuthResponseDto> Handle(ImpersonateUserCommand request, CancellationToken cancellationToken)
    {
        var target = await context.Users.FindAsync([request.UserId], cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        // A platform admin impersonating another platform admin has no legitimate
        // support use case and opens a lateral-access path between SystemAdmin
        // accounts — block it outright rather than merely logging it.
        if (target.Role == Role.SystemAdmin)
        {
            throw new ForbiddenException("SystemAdmin accounts cannot be impersonated.");
        }

        var actor = await context.Users.FindAsync([currentUser.UserId], cancellationToken);

        await auditLogger.LogAsync(
            AuditEventTypes.UserImpersonationStarted,
            $"SystemAdmin {actor?.Email ?? currentUser.UserId?.ToString()} started an impersonation session as {target.Email}.",
            target.WorkspaceId,
            target.Id,
            cancellationToken);

        var accessToken = tokenService.IssueImpersonationToken(target, ImpersonationLifetime);

        return new AuthResponseDto
        {
            Token = accessToken.Value,
            ExpiresAt = accessToken.ExpiresAt,
            User = mapper.Map<UserDto>(target)
        };
    }
}
