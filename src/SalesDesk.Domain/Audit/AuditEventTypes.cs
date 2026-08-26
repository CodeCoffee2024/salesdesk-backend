namespace SalesDesk.Domain.Audit;

/// <summary>Named event types written to the audit log — see <see cref="AuditLog"/>.</summary>
public static class AuditEventTypes
{
    public const string WorkspaceRegistered = "WorkspaceRegistered";

    public const string WorkspaceSuspended = "WorkspaceSuspended";

    public const string WorkspaceActivated = "WorkspaceActivated";

    public const string WorkspaceQuotaChanged = "WorkspaceQuotaChanged";

    public const string SystemError = "SystemError";

    public const string UserImpersonationStarted = "UserImpersonationStarted";
}
