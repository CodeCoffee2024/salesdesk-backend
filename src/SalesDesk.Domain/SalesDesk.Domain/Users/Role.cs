namespace SalesDesk.Domain.Users;

/// <summary>
/// Platform-wide and workspace-level permission tiers. Only <see cref="WorkspaceAdmin"/>
/// is assigned today (auto-granted to whoever registers a new workspace) — the other
/// values exist so RBAC enforcement (role-based authorization policies, admin console
/// access) has a stable set of names to build against without touching this entity again.
/// </summary>
public enum Role
{
    Viewer = 0,
    SalesManager = 1,
    WorkspaceAdmin = 2,
    SystemAdmin = 3
}
