using System.Security.Claims;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Users;

namespace SalesDesk.Api.Services;

/// <summary>
/// Reads the identity of the caller off the validated JWT's claims (populated by
/// TokenService.IssueToken in Infrastructure) — the only place in the app allowed
/// to touch HttpContext directly.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Role? Role
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<Role>(value, out var role) ? role : null;
        }
    }

    public Guid? WorkspaceId
    {
        get
        {
            var value = User?.FindFirstValue("workspace_id");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
