using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SalesDesk.Api.Tests;

/// <summary>
/// The PayMongo webhook endpoint's signature verification is its only security
/// boundary (it's [AllowAnonymous] by design — PayMongo's servers, not a
/// logged-in user, call it), so these tests exercise that boundary directly
/// rather than mocking it away. Mirrors StripeWebhookControllerTests.
/// </summary>
public class PayMongoWebhookControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string WebhookSecret = "whsec_test_fixed_secret_for_paymongo_signature_tests";

    private readonly WebApplicationFactory<Program> _factory;

    public PayMongoWebhookControllerTests(WebApplicationFactory<Program> factory)
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
                            ["Payments:PayMongoWebhookSecret"] = webhookSecret
                        }));
                }
            })
            .CreateClient();
        return client;
    }

    // Reproduces PayMongo's own webhook signing scheme by hand — there's no
    // official .NET SDK helper to sign as if you were PayMongo, only to verify.
    private static string SignPayload(string payload, string secret, DateTimeOffset timestamp, bool live)
    {
        var unixTimestamp = timestamp.ToUnixTimeSeconds();
        var signedPayload = $"{unixTimestamp}.{payload}";
        var signatureBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var signatureHex = Convert.ToHexStringLower(signatureBytes);
        return live ? $"t={unixTimestamp},li={signatureHex}" : $"t={unixTimestamp},te={signatureHex}";
    }

    [Fact]
    public async Task Webhook_secret_not_configured_returns_503()
    {
        var client = CreateClientWithWebhookSecret(webhookSecret: null);
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/webhooks/paymongo", content);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Missing_signature_header_returns_400()
    {
        var client = CreateClientWithWebhookSecret(WebhookSecret);
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/webhooks/paymongo", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_signature_returns_400()
    {
        var client = CreateClientWithWebhookSecret(WebhookSecret);
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        content.Headers.Add("Paymongo-Signature", "t=1000000000,te=not-a-real-signature");

        var response = await client.PostAsync("/api/webhooks/paymongo", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Correctly_signed_unresolvable_event_returns_200()
    {
        // A genuinely-signed test-mode (te) payload proves the verification
        // path works end to end; metadata with no resolvable workspaceId keeps
        // this test focused on the signature boundary, which is what
        // PayMongoWebhookController itself is responsible for — the
        // command-dispatch behavior is covered separately.
        var client = CreateClientWithWebhookSecret(WebhookSecret);
        var payload = """
            {"data":{"id":"evt_test_123","type":"event","attributes":{"type":"checkout_session.payment.paid","livemode":false,"data":{"id":"cs_test_123","attributes":{"metadata":{"workspaceId":"not-a-guid","tier":"Pro","billingCycle":"Monthly"}}}}}}
            """;
        var signature = SignPayload(payload, WebhookSecret, DateTimeOffset.UtcNow, live: false);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Paymongo-Signature", signature);

        var response = await client.PostAsync("/api/webhooks/paymongo", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Correctly_signed_event_of_an_unhandled_type_returns_200()
    {
        var client = CreateClientWithWebhookSecret(WebhookSecret);
        var payload = """{"data":{"id":"evt_test_456","type":"event","attributes":{"type":"payment.failed","livemode":false,"data":{}}}}""";
        var signature = SignPayload(payload, WebhookSecret, DateTimeOffset.UtcNow, live: false);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Paymongo-Signature", signature);

        var response = await client.PostAsync("/api/webhooks/paymongo", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
