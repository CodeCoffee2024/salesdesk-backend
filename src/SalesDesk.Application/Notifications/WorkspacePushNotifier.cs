using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Notifications;

/// <summary>
/// Fans a Web Push notification out to every device subscribed by any user in a
/// workspace (TASK-027) — every studio user who's opted in gets notified when a
/// client views, signs, or requests a revision on one of their documents, not
/// just whoever created it.
/// </summary>
public sealed class WorkspacePushNotifier(IApplicationDbContext context, IPushNotificationSender pushSender)
{
    public async Task NotifyWorkspaceAsync(Guid workspaceId, string title, string body, string url, CancellationToken cancellationToken)
    {
        var userIds = await context.Users
            .Where(u => u.WorkspaceId == workspaceId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
        {
            return;
        }

        var subscriptions = await context.PushSubscriptions
            .Where(s => userIds.Contains(s.UserId))
            .ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            await pushSender.SendAsync(
                new PushSubscriptionTarget(subscription.Endpoint, subscription.P256dhKey, subscription.AuthKey),
                title,
                body,
                url,
                cancellationToken);
        }
    }
}
