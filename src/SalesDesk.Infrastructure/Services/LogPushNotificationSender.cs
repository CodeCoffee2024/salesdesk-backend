using Microsoft.Extensions.Logging;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Stand-in <see cref="IPushNotificationSender"/> for as long as no VAPID keypair
/// is configured: logs the notification instead of sending it, mirroring
/// LogEmailSender — see docs/research/TASK-DEPLOYMENT.md.
/// </summary>
public sealed class LogPushNotificationSender(ILogger<LogPushNotificationSender> logger) : IPushNotificationSender
{
    public Task SendAsync(PushSubscriptionTarget subscription, string title, string body, string url, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Push notification (not sent — no VAPID keys configured): Title={Title} Body={Body} Url={Url}", title, body, url);

        return Task.CompletedTask;
    }
}
