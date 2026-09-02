namespace SalesDesk.Application.Common.Interfaces;

/// <summary>A hosted checkout page URL for a workspace to complete a subscription purchase — the caller redirects the browser there and never handles card/wallet details itself.</summary>
public sealed record CheckoutSession(string CheckoutUrl, string ProviderReference);

/// <summary>
/// TASK-038: creates a hosted checkout session for a workspace upgrading to a
/// paid tier. Implemented by SalesDesk.Infrastructure against whichever
/// payment provider is configured for the workspace's region — PayMongo (or
/// GCash/Maya/bank transfer via PayMongo's own channel selection) for PH
/// workspaces, Stripe or PayPal for everyone else — see DependencyInjection
/// for the fallback (UnconfiguredPaymentGatewayService) used until a real
/// provider's API credentials are configured; none are yet, so every call
/// currently fails clearly rather than pretending to charge anyone.
/// </summary>
public interface IPaymentGatewayService
{
    Task<CheckoutSession> CreateCheckoutSessionAsync(
        Guid workspaceId, string tier, string billingCycle, string currency, decimal amount, CancellationToken cancellationToken);
}
