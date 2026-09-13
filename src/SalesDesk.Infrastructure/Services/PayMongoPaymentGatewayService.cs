using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Real (non-stubbed) implementation of <see cref="IPaymentGatewayService"/> against
/// PayMongo's Checkout Sessions API (https://developers.paymongo.com/reference/the-checkout-session-object)
/// — the platform's payment gateway for Philippines-registered workspaces, offering
/// card, GCash, and Maya as payment methods within the one hosted checkout page.
/// Registered in DependencyInjection only once Payments:PayMongoSecretKey is
/// configured; the HttpClient's BaseAddress and Basic-auth header (username = secret
/// key, no password — PayMongo's own convention, same shape as Stripe's) are both set
/// there. Not usable for a non-PHP workspace: PayMongo settles in PHP only, so a
/// Global-catalog checkout (USD) fails clearly rather than silently misbilling.
///
/// The workspace/tier/billingCycle this session is for travels in PayMongo's own
/// `metadata` field rather than a row this app persists up front — PayMongoWebhookController
/// reads it straight back off the `checkout_session.payment.paid` event once payment
/// completes, so there's nothing to reconcile or clean up for an abandoned session.
/// </summary>
public sealed class PayMongoPaymentGatewayService(HttpClient httpClient, IPublicLinkBuilder linkBuilder) : IPaymentGatewayService
{
    public async Task<CheckoutSession> CreateCheckoutSessionAsync(
        Guid workspaceId, string tier, string billingCycle, string currency, decimal amount, CancellationToken cancellationToken)
    {
        if (!string.Equals(currency, "PHP", StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentGatewayUnavailableException(
                "Card checkout isn't available for this workspace's region yet — use \"Request upgrade\" instead and we'll arrange billing directly.");
        }

        var periodLabel = billingCycle == "Annual" ? "Annual" : "Monthly";
        var lineItemName = $"SalesDesk Full Access — {periodLabel}";

        var requestBody = new PayMongoCheckoutSessionRequest(new(new(
            LineItems: [new(Amount: (int)Math.Round(amount * 100m), Currency: "PHP", Name: lineItemName, Quantity: 1)],
            PaymentMethodTypes: ["card", "gcash", "paymaya"],
            Description: lineItemName,
            SendEmailReceipt: false,
            ShowLineItems: true,
            CancelUrl: linkBuilder.BuildBillingCheckoutResultUrl(succeeded: false),
            SuccessUrl: linkBuilder.BuildBillingCheckoutResultUrl(succeeded: true),
            Metadata: new(workspaceId.ToString("D"), tier, billingCycle))));

        using var response = await httpClient.PostAsJsonAsync("checkout_sessions", requestBody, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PaymentGatewayUnavailableException($"PayMongo checkout session creation failed ({(int)response.StatusCode}): {errorBody}");
        }

        var parsed = await response.Content.ReadFromJsonAsync<PayMongoCheckoutSessionResponse>(cancellationToken)
            ?? throw new PaymentGatewayUnavailableException("PayMongo returned an empty checkout session response.");

        return new CheckoutSession(parsed.Data.Attributes.CheckoutUrl, parsed.Data.Id);
    }

    private sealed record PayMongoCheckoutSessionRequest([property: JsonPropertyName("data")] PayMongoRequestData Data);

    private sealed record PayMongoRequestData([property: JsonPropertyName("attributes")] PayMongoRequestAttributes Attributes);

    private sealed record PayMongoRequestAttributes(
        [property: JsonPropertyName("line_items")] List<PayMongoLineItem> LineItems,
        [property: JsonPropertyName("payment_method_types")] List<string> PaymentMethodTypes,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("send_email_receipt")] bool SendEmailReceipt,
        [property: JsonPropertyName("show_line_items")] bool ShowLineItems,
        [property: JsonPropertyName("cancel_url")] string CancelUrl,
        [property: JsonPropertyName("success_url")] string SuccessUrl,
        [property: JsonPropertyName("metadata")] PayMongoCheckoutMetadata Metadata);

    private sealed record PayMongoLineItem(
        [property: JsonPropertyName("amount")] int Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("quantity")] int Quantity);

    private sealed record PayMongoCheckoutSessionResponse([property: JsonPropertyName("data")] PayMongoResponseData Data);

    private sealed record PayMongoResponseData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("attributes")] PayMongoResponseAttributes Attributes);

    private sealed record PayMongoResponseAttributes([property: JsonPropertyName("checkout_url")] string CheckoutUrl);
}

/// <summary>The metadata a PayMongo checkout session carries and echoes back on its webhook events — how PayMongoWebhookController learns which workspace/tier/cycle to activate, with no separate row to persist and reconcile. Public so PayMongoWebhookController can deserialize the same shape.</summary>
public sealed record PayMongoCheckoutMetadata(
    [property: JsonPropertyName("workspaceId")] string WorkspaceId,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("billingCycle")] string BillingCycle);
