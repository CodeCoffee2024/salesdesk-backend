namespace SalesDesk.Application.Common.Interfaces;

public sealed record PushSubscriptionTarget(string Endpoint, string P256dhKey, string AuthKey);

/// <summary>
/// Sends a single Web Push message (TASK-027) to one subscribed browser. Implemented
/// in Infrastructure against the VAPID keypair configured via WebPush:VapidPublicKey /
/// WebPush:VapidPrivateKey; a log-only fallback is registered when no keypair is
/// configured, mirroring the IEmailSender pattern, so a deploy without push
/// credentials configured yet doesn't fail every document view/sign.
/// </summary>
public interface IPushNotificationSender
{
    Task SendAsync(PushSubscriptionTarget subscription, string title, string body, string url, CancellationToken cancellationToken);
}
