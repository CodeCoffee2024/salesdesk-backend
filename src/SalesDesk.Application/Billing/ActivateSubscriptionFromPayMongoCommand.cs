using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Billing;

/// <summary>
/// Activates a paid tier once PayMongoWebhookController receives a
/// `checkout_session.payment.paid` event — the one real, automatic (no admin
/// click needed) upgrade path, as opposed to the manual GCash/upgrade-request
/// flows that still require a human to approve. Idempotent the same way those
/// two are: a workspace already on this tier with a later (or equal) expiry is
/// left alone rather than re-extended, so a duplicate webhook delivery
/// (PayMongo retries on anything but a 2xx) can't push the expiry out twice.
/// </summary>
public sealed record ActivateSubscriptionFromPayMongoCommand(Guid WorkspaceId, SubscriptionTier Tier, string BillingCycle, string ProviderReference)
    : IRequest;

public sealed class ActivateSubscriptionFromPayMongoCommandHandler(IApplicationDbContext context, IDateTime dateTime, IEmailSender emailSender)
    : IRequestHandler<ActivateSubscriptionFromPayMongoCommand>
{
    public async Task Handle(ActivateSubscriptionFromPayMongoCommand request, CancellationToken cancellationToken)
    {
        var workspace = await context.Workspaces.SingleOrDefaultAsync(w => w.Id == request.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            // A workspace that no longer exists — nothing to activate, and
            // nothing PayMongo retrying this event again would ever resolve.
            return;
        }

        var now = dateTime.UtcNow;
        var newExpiresAtUtc = now + (request.BillingCycle == "Annual" ? TimeSpan.FromDays(365) : TimeSpan.FromDays(30));

        if (workspace.SubscriptionTier == request.Tier && workspace.SubscriptionEndDate is { } currentExpiry && currentExpiry >= newExpiresAtUtc)
        {
            return;
        }

        workspace.ActivatePaidSubscription(request.Tier, newExpiresAtUtc);
        await context.SaveChangesAsync(cancellationToken);

        var body = $"""
            <p>Hi {workspace.Name},</p>
            <p>Your payment went through — your workspace is now on the <strong>{request.Tier}</strong> plan.</p>
            <p>This subscription is active through <strong>{newExpiresAtUtc:MMM d, yyyy}</strong>.</p>
            """;

        await emailSender.SendAsync(
            new EmailMessage(workspace.Email, Cc: null, $"You're on {request.Tier} — subscription confirmed", EmailBranding.System(body)),
            cancellationToken);
    }
}
