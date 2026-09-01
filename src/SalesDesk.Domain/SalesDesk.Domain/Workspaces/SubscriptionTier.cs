namespace SalesDesk.Domain.Workspaces;

/// <summary>
/// Billing tier for a workspace (TASK-031). Every workspace starts <see cref="Free"/>;
/// <see cref="Premium"/> is granted either by the "Early 100 Free Year" promo
/// (RegisterCommandHandler, via <see cref="Workspace.GrantEarlyBirdPremium"/>) or,
/// once real paid billing exists, a standard upgrade — there is deliberately no
/// separate Subscription/Billing entity yet, so this lives directly on Workspace,
/// the platform's one billing unit.
/// </summary>
public enum SubscriptionTier
{
    Free = 0,
    Premium = 1
}
