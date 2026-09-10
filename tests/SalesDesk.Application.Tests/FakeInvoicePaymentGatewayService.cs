using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Tests;

/// <summary>Configurable test double for IInvoicePaymentGatewayService — returns a canned session by default, or throws PaymentGatewayUnavailableException when ThrowUnavailable is set, simulating an unconfigured/misbehaving Stripe integration.</summary>
public sealed class FakeInvoicePaymentGatewayService : IInvoicePaymentGatewayService
{
    public bool ThrowUnavailable { get; set; }

    public string CheckoutUrl { get; set; } = "https://checkout.stripe.test/session/cs_test_fake";

    public string ProviderReference { get; set; } = "cs_test_fake";

    public Guid? LastDocumentId { get; private set; }

    public Task<CheckoutSession> CreateCheckoutSessionAsync(
        Guid documentId, string documentNumber, decimal amount, string currency, string customerEmail, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        LastDocumentId = documentId;

        if (ThrowUnavailable)
        {
            throw new PaymentGatewayUnavailableException("Paying this invoice online isn't available yet — payment processing isn't configured on this server.");
        }

        return Task.FromResult(new CheckoutSession(CheckoutUrl, ProviderReference));
    }
}
