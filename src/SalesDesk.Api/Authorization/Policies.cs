using SalesDesk.Domain.Users;

namespace SalesDesk.Api.Authorization;

/// <summary>
/// Named authorization policies (TASK-016), registered against <see cref="Role"/>
/// in Program.cs. Controllers reference these names instead of hand-typed role
/// strings so the create/edit vs. delete split stays in one place.
/// </summary>
public static class Policies
{
    /// <summary>Create/edit/status-change/set-default actions — everyone except Viewer.</summary>
    public const string CanManage = "CanManage";

    /// <summary>Delete actions — full workspace control only, not SalesManager.</summary>
    public const string CanDelete = "CanDelete";
}
