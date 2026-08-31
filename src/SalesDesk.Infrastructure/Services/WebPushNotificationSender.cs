using System.Text.Json;
using Microsoft.Extensions.Logging;
using SalesDesk.Application.Common.Interfaces;
using WebPush;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Sends real Web Push notifications (TASK-027) via the WebPush library once a
/// VAPID keypair is configured — see DependencyInjection, which only registers
/// this in place of LogPushNotificationSender when both keys are present.
/// </summary>
public sealed class WebPushNotificationSender(VapidDetails vapidDetails, ILogger<WebPushNotificationSender> logger) : IPushNotificationSender
{
    private readonly WebPushClient _client = new();

    public async Task SendAsync(PushSubscriptionTarget subscription, string title, string body, string url, CancellationToken cancellationToken)
    {
        var pushSubscription = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dhKey, subscription.AuthKey);
        var payload = JsonSerializer.Serialize(new { title, body, url });

        try
        {
            await _client.SendNotificationAsync(pushSubscription, payload, vapidDetails, cancellationToken: cancellationToken);
        }
        catch (WebPushException ex)
        {
            // A stale/revoked subscription (the browser uninstalled the PWA, the
            // user cleared site data, etc.) surfaces as 404/410 here — logged and
            // swallowed rather than failing the document view/sign/revision
            // request that triggered it; the subscription itself is left in place
            // rather than guessing at cleanup from a single failed send.
            logger.LogWarning(ex, "Web Push send failed ({StatusCode}) for endpoint {Endpoint}", ex.StatusCode, subscription.Endpoint);
        }
    }
}
