namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// TASK-042: creates a hosted Stripe Checkout session for a client paying a
/// single invoice online. Deliberately separate from <see cref="IPaymentGatewayService"/>
/// (TASK-038's workspace subscription-tier checkout) — the two are unrelated
/// purchases (a one-time invoice payment vs. a recurring workspace
/// subscription), keyed on different entities, and this one must ship
/// independently of whenever/whether subscription billing gets a real
/// provider. Reuses <see cref="CheckoutSession"/> since the result shape
/// (a hosted URL plus a provider reference) is identical either way.
/// Implemented in SalesDesk.Infrastructure; falls back to an "unconfigured"
/// stub (throwing <see cref="Exceptions.PaymentGatewayUnavailableException"/>)
/// until a real Stripe secret key is configured — see DependencyInjection. See
/// <see cref="SalesDesk.Application.Common.Exceptions.PaymentGatewayUnavailableException"/>.
/// </summary>
public interface IInvoicePaymentGatewayService
{
    Task<CheckoutSession> CreateCheckoutSessionAsync(
        Guid documentId,
        string documentNumber,
        decimal amount,
        string currency,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken);
}
