using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Billing;

namespace SalesDesk.Application.Billing;

public sealed record ApproveGCashPaymentResultDto(string WorkspaceName, string Tier, string BillingCycle, DateTimeOffset ExpiresAtUtc, bool WasAlreadyApproved);

/// <summary>TASK-039: the one-click link in the GCash payment admin-notification email. Idempotent — see GCashPaymentSubmission.Approve.</summary>
public sealed record ApproveGCashPaymentCommand(string Token) : IRequest<ApproveGCashPaymentResultDto>;

public sealed class ApproveGCashPaymentCommandHandler(IApplicationDbContext context, IDateTime dateTime, IEmailSender emailSender)
    : IRequestHandler<ApproveGCashPaymentCommand, ApproveGCashPaymentResultDto>
{
    public async Task<ApproveGCashPaymentResultDto> Handle(ApproveGCashPaymentCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = SecureTokens.Hash(request.Token);
        var submission = await context.GCashPaymentSubmissions
            .FirstOrDefaultAsync(s => s.ApprovalTokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException(nameof(GCashPaymentSubmission), request.Token);

        var workspace = await context.Workspaces.SingleAsync(w => w.Id == submission.WorkspaceId, cancellationToken);

        var wasAlreadyApproved = submission.IsApproved;
        var now = dateTime.UtcNow;
        var expiresAtUtc = now + submission.SubscriptionLength;

        if (!wasAlreadyApproved)
        {
            submission.Approve(now);
            workspace.ActivatePaidSubscription(submission.Tier, expiresAtUtc);
            await context.SaveChangesAsync(cancellationToken);

            await SendConfirmationAsync(workspace, submission, expiresAtUtc, cancellationToken);
        }
        else
        {
            // Re-derive from the already-recorded approval rather than a fresh
            // "now" — a second visit to this link shouldn't push the expiration
            // date out further.
            expiresAtUtc = workspace.SubscriptionEndDate ?? expiresAtUtc;
        }

        return new ApproveGCashPaymentResultDto(workspace.Name, submission.Tier.ToString(), submission.BillingCycle, expiresAtUtc, wasAlreadyApproved);
    }

    private async Task SendConfirmationAsync(Domain.Workspaces.Workspace workspace, GCashPaymentSubmission submission, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        var body = $"""
            <p>Hi {workspace.Name},</p>
            <p>Your GCash payment (reference <strong>{submission.GCashReferenceNumber}</strong>) has been verified — your workspace is now on the <strong>{submission.Tier}</strong> plan.</p>
            <p>This subscription is active through <strong>{expiresAtUtc:MMM d, yyyy}</strong>.</p>
            """;

        await emailSender.SendAsync(
            new EmailMessage(workspace.Email, Cc: null, $"You're on {submission.Tier} — subscription confirmed", EmailBranding.System(body)),
            cancellationToken);
    }
}
