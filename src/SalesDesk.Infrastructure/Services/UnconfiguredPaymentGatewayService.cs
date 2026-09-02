using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>Registered until a real payment provider (PayMongo/Stripe/PayPal) has configured credentials (see DependencyInjection). Fails clearly instead of returning a fake checkout URL that would silently go nowhere.</summary>
public sealed class UnconfiguredPaymentGatewayService : IPaymentGatewayService
{
    public Task<CheckoutSession> CreateCheckoutSessionAsync(
        Guid workspaceId, string tier, string billingCycle, string currency, decimal amount, CancellationToken cancellationToken) =>
        throw new PaymentGatewayUnavailableException("Paid upgrades aren't available yet — payment processing isn't configured on this server.");
}
