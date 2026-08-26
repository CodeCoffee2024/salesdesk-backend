using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Common.Extensions;

public static class CurrentUserServiceExtensions
{
    /// <summary>
    /// The authenticated caller's workspace id. Every controller requires
    /// authentication (see Program.cs's global RequireAuthorization()), so a request
    /// that reaches a handler without a workspace_id claim indicates a malformed or
    /// tampered token rather than a normal unauthenticated call — treated the same
    /// way (401) as any other authentication failure.
    /// </summary>
    public static Guid RequireWorkspaceId(this ICurrentUserService currentUser) =>
        currentUser.WorkspaceId ?? throw new UnauthorizedAccessException("No workspace associated with the current user.");
}
