using Microsoft.Extensions.Logging;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// TASK-042: real Stripe Checkout integration for paying a single invoice
/// online — mode=payment (one-time), not a subscription. Uses an explicit
/// <see cref="StripeClient"/> instance rather than the static
/// <see cref="StripeConfiguration.ApiKey"/> field, so the secret key stays a
/// per-instance dependency instead of mutable process-wide state.
/// </summary>
public sealed class StripeInvoicePaymentGatewayService(StripeClient stripeClient, ILogger<StripeInvoicePaymentGatewayService> logger)
    : IInvoicePaymentGatewayService
{
    public async Task<CheckoutSession> CreateCheckoutSessionAsync(
        Guid documentId, string documentNumber, decimal amount, string currency, string customerEmail, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            // Both set: ClientReferenceId is Stripe's own purpose-built "your
            // internal id" field and survives onto the checkout.session.completed
            // event with no extra API call; Metadata carries the same value as a
            // fallback, and is also visible on the underlying PaymentIntent.
            ClientReferenceId = documentId.ToString(),
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmount = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Invoice {documentNumber}"
                        }
                    }
                }
            ],
            Metadata = new Dictionary<string, string> { ["documentId"] = documentId.ToString() }
        };

        try
        {
            var service = new SessionService(stripeClient);
            var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
            return new CheckoutSession(session.Url, session.Id);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe Checkout session creation failed for document {DocumentId}", documentId);
            throw new PaymentGatewayUnavailableException("Starting payment for this invoice failed. Please try again shortly.");
        }
    }
}
