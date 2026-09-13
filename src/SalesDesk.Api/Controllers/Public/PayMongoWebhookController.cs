using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SalesDesk.Application.Billing;
using SalesDesk.Domain.Workspaces;
using SalesDesk.Infrastructure.Services;

namespace SalesDesk.Api.Controllers.Public;

/// <summary>
/// Receives PayMongo's server-to-server webhook deliveries for the Full Access
/// subscription checkout flow (PayMongoPaymentGatewayService). [AllowAnonymous]
/// is deliberate and safe, same as StripeWebhookController — PayMongo's servers
/// call this, not a logged-in user, and the HMAC signature verification below
/// (not authentication) is the endpoint's entire security boundary.
/// </summary>
[ApiController]
[Route("api/webhooks/paymongo")]
public sealed class PayMongoWebhookController(ISender sender, IConfiguration configuration, ILogger<PayMongoWebhookController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        var webhookSecret = configuration["Payments:PayMongoWebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var signatureHeader = Request.Headers["Paymongo-Signature"].ToString();
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return BadRequest();
        }

        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);

        if (!IsValidSignature(json, signatureHeader, webhookSecret))
        {
            logger.LogWarning("PayMongo webhook signature verification failed");
            return BadRequest();
        }

        PayMongoEventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PayMongoEventEnvelope>(json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "PayMongo webhook payload could not be parsed");
            return BadRequest();
        }

        var eventType = envelope?.Data?.Attributes?.Type;
        var checkoutSession = envelope?.Data?.Attributes?.Data;
        if (eventType == "checkout_session.payment.paid" && checkoutSession is not null)
        {
            await HandleCheckoutSessionPaidAsync(checkoutSession, cancellationToken);
        }

        // Any 2xx tells PayMongo "received" — every event type we don't act on
        // is a deliberate no-op, not an error, so it isn't retried pointlessly.
        return Ok();
    }

    private async Task HandleCheckoutSessionPaidAsync(PayMongoCheckoutSessionPayload checkoutSession, CancellationToken cancellationToken)
    {
        var metadata = checkoutSession.Attributes?.Metadata;
        if (metadata is null
            || !Guid.TryParse(metadata.WorkspaceId, out var workspaceId)
            || !Enum.TryParse<SubscriptionTier>(metadata.Tier, out var tier)
            || tier == SubscriptionTier.Free)
        {
            // A payload we can never reconcile to a workspace/tier — logging and
            // returning 200 is correct here: an error would just make PayMongo
            // retry an event that can never resolve differently.
            logger.LogWarning("PayMongo checkout_session.payment.paid event {SessionId} had unresolvable metadata", checkoutSession.Id);
            return;
        }

        await sender.Send(
            new ActivateSubscriptionFromPayMongoCommand(workspaceId, tier, metadata.BillingCycle, checkoutSession.Id),
            cancellationToken);
    }

    /// <summary>
    /// PayMongo signs as `Paymongo-Signature: t=&lt;unix-timestamp&gt;,te=&lt;test-mode-hex-hmac&gt;,li=&lt;live-mode-hex-hmac&gt;`
    /// — the HMAC-SHA256 (hex) of `{timestamp}.{rawBody}` keyed on the endpoint's
    /// webhook secret. Which of te/li is populated depends on whether the event
    /// was generated in test or live mode, and that mode isn't known ahead of
    /// verifying, so this accepts a match against either field rather than
    /// picking one — only the field matching the mode the secret was actually
    /// issued for will ever verify correctly either way.
    /// </summary>
    private static bool IsValidSignature(string rawBody, string signatureHeader, string webhookSecret)
    {
        string? timestamp = null;
        string? testSignature = null;
        string? liveSignature = null;

        foreach (var part in signatureHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }

            switch (kv[0])
            {
                case "t": timestamp = kv[1]; break;
                case "te": testSignature = kv[1]; break;
                case "li": liveSignature = kv[1]; break;
            }
        }

        if (timestamp is null || (testSignature is null && liveSignature is null))
        {
            return false;
        }

        var signedPayload = $"{timestamp}.{rawBody}";
        var computedBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), Encoding.UTF8.GetBytes(signedPayload));
        var computedHex = Convert.ToHexStringLower(computedBytes);

        return (testSignature is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(testSignature)))
            || (liveSignature is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(liveSignature)));
    }

    private sealed record PayMongoEventEnvelope([property: JsonPropertyName("data")] PayMongoEventData? Data);

    private sealed record PayMongoEventData([property: JsonPropertyName("attributes")] PayMongoEventAttributes? Attributes);

    private sealed record PayMongoEventAttributes(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("data")] PayMongoCheckoutSessionPayload? Data);

    private sealed record PayMongoCheckoutSessionPayload(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("attributes")] PayMongoCheckoutSessionAttributes? Attributes);

    private sealed record PayMongoCheckoutSessionAttributes([property: JsonPropertyName("metadata")] PayMongoCheckoutMetadata? Metadata);
}
