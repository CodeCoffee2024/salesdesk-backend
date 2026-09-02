namespace SalesDesk.Domain.Workspaces;

/// <summary>
/// Billing tier for a workspace (TASK-031, extended by TASK-038's regional
/// pricing catalog). Every workspace starts <see cref="Free"/>; <see cref="Pro"/>
/// is granted either by the "Early 100 Free Year" promo (RegisterCommandHandler,
/// via <see cref="Workspace.GrantEarlyBirdPro"/>) or, once real paid billing
/// exists, a standard upgrade to either paid tier — there is deliberately no
/// separate Subscription/Billing entity yet, so this lives directly on Workspace,
/// the platform's one billing unit. Named <see cref="Pro"/> rather than the
/// original "Premium": TASK-038 introduced a second, higher paid tier
/// (<see cref="Studio"/>), and "Premium" stopped being a clear name once it
/// wasn't the only paid option.
/// </summary>
public enum SubscriptionTier
{
    Free = 0,
    Pro = 1,
    Studio = 2
}
