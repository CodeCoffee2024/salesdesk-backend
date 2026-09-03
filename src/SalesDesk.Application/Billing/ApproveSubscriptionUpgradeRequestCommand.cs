using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Billing;

namespace SalesDesk.Application.Billing;

public sealed record ApproveSubscriptionUpgradeRequestResultDto(string WorkspaceName, string Tier, string BillingCycle, DateTimeOffset ExpiresAtUtc, bool WasAlreadyApproved);

/// <summary>The one-click link in the upgrade-request admin-notification email. Idempotent — see SubscriptionUpgradeRequest.Approve.</summary>
public sealed record ApproveSubscriptionUpgradeRequestCommand(string Token) : IRequest<ApproveSubscriptionUpgradeRequestResultDto>;

public sealed class ApproveSubscriptionUpgradeRequestCommandHandler(IApplicationDbContext context, IDateTime dateTime, IEmailSender emailSender)
    : IRequestHandler<ApproveSubscriptionUpgradeRequestCommand, ApproveSubscriptionUpgradeRequestResultDto>
{
    public async Task<ApproveSubscriptionUpgradeRequestResultDto> Handle(ApproveSubscriptionUpgradeRequestCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = SecureTokens.Hash(request.Token);
        var upgradeRequest = await context.SubscriptionUpgradeRequests
            .FirstOrDefaultAsync(r => r.ApprovalTokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionUpgradeRequest), request.Token);

        var workspace = await context.Workspaces.SingleAsync(w => w.Id == upgradeRequest.WorkspaceId, cancellationToken);

        var wasAlreadyApproved = upgradeRequest.IsApproved;
        var now = dateTime.UtcNow;
        var expiresAtUtc = now + upgradeRequest.SubscriptionLength;

        if (!wasAlreadyApproved)
        {
            upgradeRequest.Approve(now);
            workspace.ActivatePaidSubscription(upgradeRequest.Tier, expiresAtUtc);
            await context.SaveChangesAsync(cancellationToken);

            await SendConfirmationAsync(workspace, upgradeRequest, expiresAtUtc, cancellationToken);
        }
        else
        {
            // Re-derive from the already-recorded approval rather than a fresh
            // "now" — a second visit to this link shouldn't push the expiration
            // date out further.
            expiresAtUtc = workspace.SubscriptionEndDate ?? expiresAtUtc;
        }

        return new ApproveSubscriptionUpgradeRequestResultDto(workspace.Name, upgradeRequest.Tier.ToString(), upgradeRequest.BillingCycle, expiresAtUtc, wasAlreadyApproved);
    }

    private async Task SendConfirmationAsync(Domain.Workspaces.Workspace workspace, SubscriptionUpgradeRequest upgradeRequest, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        var body = $"""
            <p>Hi {workspace.Name},</p>
            <p>Your upgrade request has been approved — your workspace is now on the <strong>{upgradeRequest.Tier}</strong> plan.</p>
            <p>This subscription is active through <strong>{expiresAtUtc:MMM d, yyyy}</strong>.</p>
            """;

        await emailSender.SendAsync(
            new EmailMessage(workspace.Email, Cc: null, $"You're on {upgradeRequest.Tier} — subscription confirmed", EmailBranding.System(body)),
            cancellationToken);
    }
}
