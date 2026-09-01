using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Users;

namespace SalesDesk.Application.Tests;

public sealed class FakeCurrentUserService(Guid? workspaceId, Guid? userId = null, Role role = Role.WorkspaceAdmin, bool isAuthenticated = true)
    : ICurrentUserService
{
    public bool IsAuthenticated { get; } = isAuthenticated;

    public Guid? UserId { get; } = isAuthenticated ? userId ?? Guid.NewGuid() : null;

    public Role? Role { get; } = isAuthenticated ? role : null;

    public Guid? WorkspaceId { get; } = workspaceId;

    /// <summary>Defaults true so existing tests that don't care about TASK-030's gate (e.g. CompleteOnboarding) aren't affected by it — set false to exercise EmailVerificationBehavior.</summary>
    public bool IsEmailVerified { get; set; } = true;
}
