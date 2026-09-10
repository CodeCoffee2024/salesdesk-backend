using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SalesDesk.Api.Tests;

/// <summary>
/// TASK-042: the Stripe webhook endpoint's signature verification is its only
/// security boundary (it's [AllowAnonymous] by design — Stripe's servers, not a
/// logged-in user, call it), so these tests exercise that boundary directly
/// rather than mocking it away.
/// </summary>
public class StripeWebhookControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string WebhookSecret = "whsec_test_fixed_secret_for_signature_verification_tests";

    private readonly WebApplicationFactory<Program> _factory;

    public StripeWebhookControllerTests(WebApplicationFactory<Program> factory)
        => _factory = factory;

    private HttpClient CreateClientWithWebhookSecret(string? webhookSecret)
    {
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                if (webhookSecret is not null)
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Payments:StripeWebhookSecret"] = webhookSecret
                        }));
                }
            })
            .CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    // Reproduces Stripe's own webhook signing scheme (documented at
    // https://docs.stripe.com/webhooks#verify-manually) by hand: Stripe.net has
    // no built-in "sign as if you were Stripe" test helper, only ConstructEvent
    // for verifying.
    private static string SignPayload(string payload, string secret, DateTimeOffset timestamp)
    {
        var unixTimestamp = timestamp.ToUnixTimeSeconds();
        var signedPayload = $"{unixTimestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var signatureHex = Convert.ToHexString(signatureBytes).ToLowerInvariant();
        return $"t={unixTimestamp},v1={signatureHex}";
    }

    [Fact]
    public async Task Webhook_secret_not_configured_returns_503()
    {
        var client = CreateClientWithWebhookSecret(webhookSecret: null);
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/webhooks/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Missing_signature_header_returns_400()
    {
        var client = CreateClientWithWebhookSecret(WebhookSecret);
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/webhooks/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_signature_returns_400()
    {
        var client = CreateClientWithWebhookSecret(WebhookSecret);
        var payload = """{"id":"evt_test","type":"checkout.session.completed","data":{"object":{}}}""";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "t=1000000000,v1=not-a-real-signature");

        var response = await client.PostAsync("/api/webhooks/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Correctly_signed_unresolvable_event_returns_200()
    {
        // A genuinely-signed payload proves the verification path works end to
        // end; using an event with no resolvable documentId (rather than seeding
        // a real document) keeps this test focused on the signature boundary,
        // which is what StripeWebhookController itself is responsible for — the
        // command-dispatch behavior is already covered by
        // RecordDocumentPaymentCommandHandlerTests.
        var client = CreateClientWithWebhookSecret(WebhookSecret);
        var payload = """{"id":"evt_test_123","type":"checkout.session.completed","api_version":"2025-01-01","data":{"object":{"id":"cs_test_123","object":"checkout.session"}}}""";
        var signature = SignPayload(payload, WebhookSecret, DateTimeOffset.UtcNow);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", signature);

        var response = await client.PostAsync("/api/webhooks/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Correctly_signed_event_of_an_unhandled_type_returns_200()
    {
        var client = CreateClientWithWebhookSecret(WebhookSecret);
        var payload = """{"id":"evt_test_456","type":"customer.created","api_version":"2025-01-01","data":{"object":{}}}""";
        var signature = SignPayload(payload, WebhookSecret, DateTimeOffset.UtcNow);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", signature);

        var response = await client.PostAsync("/api/webhooks/stripe", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
