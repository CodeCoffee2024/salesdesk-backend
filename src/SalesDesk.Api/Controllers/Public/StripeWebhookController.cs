using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SalesDesk.Application.Documents;
using Stripe;

namespace SalesDesk.Api.Controllers.Public;

/// <summary>
/// TASK-042: receives Stripe's server-to-server webhook deliveries for the
/// Invoice Pay Now checkout flow. [AllowAnonymous] is deliberate and safe —
/// Stripe's servers call this, not a logged-in user, and the HMAC signature
/// verification below (not authentication) is the endpoint's entire security
/// boundary.
/// </summary>
[ApiController]
[Route("api/webhooks/stripe")]
public sealed class StripeWebhookController(ISender sender, IConfiguration configuration, ILogger<StripeWebhookController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        var webhookSecret = configuration["Payments:StripeWebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signatureHeader))
        {
            // Stripe.net's EventUtility.ParseStripeSignature throws a raw
            // NullReferenceException (not StripeException) for a missing header
            // rather than failing cleanly — guard here instead of relying on it.
            return BadRequest();
        }

        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);

        Event stripeEvent;
        try
        {
            // throwOnApiVersionMismatch: false — the Stripe account's pinned API
            // version won't always match whatever this SDK was compiled against,
            // and that alone isn't a reason to reject an otherwise validly-signed
            // event.
            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest();
        }

        if (stripeEvent.Type == "checkout.session.completed" && stripeEvent.Data.Object is Stripe.Checkout.Session session)
        {
            await HandleCheckoutSessionCompletedAsync(session, cancellationToken);
        }

        // Any 2xx tells Stripe "received" — every event type we don't act on is a
        // deliberate no-op, not an error, so it isn't retried pointlessly.
        return Ok();
    }

    private async Task HandleCheckoutSessionCompletedAsync(Stripe.Checkout.Session session, CancellationToken cancellationToken)
    {
        var documentIdValue = session.ClientReferenceId;
        if (string.IsNullOrWhiteSpace(documentIdValue))
        {
            session.Metadata?.TryGetValue("documentId", out documentIdValue);
        }

        if (!Guid.TryParse(documentIdValue, out var documentId))
        {
            // A payload we can never reconcile to a document — logging and
            // returning 200 is correct here: returning an error would just make
            // Stripe retry a event that can never be resolved differently.
            logger.LogWarning("Stripe checkout.session.completed event {EventId} had no resolvable documentId", session.Id);
            return;
        }

        var amountPaid = (session.AmountTotal ?? 0) / 100m;
        var currency = session.Currency?.ToUpperInvariant() ?? "USD";

        await sender.Send(
            new RecordDocumentPaymentCommand(documentId, amountPaid, currency, session.Id, DateTime.UtcNow),
            cancellationToken);
    }
}
