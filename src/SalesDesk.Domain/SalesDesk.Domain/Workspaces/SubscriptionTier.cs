namespace SalesDesk.Domain.Workspaces;

/// <summary>
/// Billing tier for a workspace (TASK-031, extended by TASK-038's regional
/// pricing catalog). Every workspace starts <see cref="Free"/>; <see cref="Pro"/>
/// ("Full Access" in every user-facing label — see PricingCatalog.DisplayName)
/// is granted by the 7-day free trial (RegisterCommandHandler, via
/// <see cref="Workspace.StartFreeTrial"/>), the "Early 100 Free Year" promo
/// (<see cref="Workspace.GrantEarlyBirdPro"/>), or a standard paid upgrade — there
/// is deliberately no separate Subscription/Billing entity yet, so this lives
/// directly on Workspace, the platform's one billing unit.
///
/// There used to be a second, higher paid tier (Studio) — collapsed back into
/// Pro/Full Access (its multi-user RBAC, custom-domain portal, and automation
/// features folded into the one paid tier) when the pricing matrix simplified to
/// just Free and Full Access. A workspace still holding the old "Studio" string
/// value is migrated to Pro by the <c>CollapseStudioTierIntoPro</c> migration.
/// </summary>
public enum SubscriptionTier
{
    Free = 0,
    Pro = 1
}
