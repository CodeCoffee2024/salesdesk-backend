using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>Registered until a real Stripe secret key is configured (see DependencyInjection). Fails clearly instead of returning a fake checkout URL that would silently go nowhere.</summary>
public sealed class UnconfiguredInvoicePaymentGatewayService : IInvoicePaymentGatewayService
{
    public Task<CheckoutSession> CreateCheckoutSessionAsync(
        Guid documentId, string documentNumber, decimal amount, string currency, string customerEmail, string successUrl, string cancelUrl, CancellationToken cancellationToken) =>
        throw new PaymentGatewayUnavailableException("Paying this invoice online isn't available yet — payment processing isn't configured on this server.");
}
