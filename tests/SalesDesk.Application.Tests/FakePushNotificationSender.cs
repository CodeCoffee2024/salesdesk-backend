using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Tests;

public sealed record SentPushNotification(PushSubscriptionTarget Subscription, string Title, string Body, string Url);

public sealed class FakePushNotificationSender : IPushNotificationSender
{
    public List<SentPushNotification> SentNotifications { get; } = [];

    public Task SendAsync(PushSubscriptionTarget subscription, string title, string body, string url, CancellationToken cancellationToken)
    {
        SentNotifications.Add(new SentPushNotification(subscription, title, body, url));
        return Task.CompletedTask;
    }
}
