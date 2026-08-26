using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Users;

namespace SalesDesk.Application.Tests;

public sealed class FakeCurrentUserService(Guid workspaceId, Guid? userId = null, Role role = Role.WorkspaceAdmin) : ICurrentUserService
{
    public bool IsAuthenticated => true;

    public Guid? UserId { get; } = userId ?? Guid.NewGuid();

    public Role? Role { get; } = role;

    public Guid? WorkspaceId { get; } = workspaceId;
}
