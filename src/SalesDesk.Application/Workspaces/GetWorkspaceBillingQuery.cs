using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Billing;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Workspaces;

/// <summary>TASK-031: backs GET /api/workspace/billing — the current user's own workspace subscription tier and early-bird promo status, for the /settings/billing page.</summary>
public sealed record GetWorkspaceBillingQuery : IRequest<WorkspaceBillingDto>;

public sealed class GetWorkspaceBillingQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, IDateTime dateTime)
    : IRequestHandler<GetWorkspaceBillingQuery, WorkspaceBillingDto>
{
    public async Task<WorkspaceBillingDto> Handle(GetWorkspaceBillingQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var workspace = await context.Workspaces.SingleAsync(w => w.Id == workspaceId, cancellationToken);

        var today = DateOnly.FromDateTime(dateTime.UtcNow.Date);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var documentsIssuedThisMonth = await context.Documents
            .CountAsync(d => d.WorkspaceId == workspaceId && d.IssueDate >= monthStart, cancellationToken);

        // Most recent first: a resubmission after an issue should show as the
        // current pending claim, not an older one. Ordered client-side rather
        // than via OrderBy in the query itself — realistically 0-1 unapproved
        // rows per workspace, and Sqlite (the test fixture's provider) can't
        // translate ORDER BY on a DateTimeOffset column the way Postgres can.
        var pendingSubmission = (await context.GCashPaymentSubmissions
            .Where(s => s.WorkspaceId == workspaceId && !s.IsApproved)
            .ToListAsync(cancellationToken))
            .OrderByDescending(s => s.SubmittedAtUtc)
            .FirstOrDefault();

        return new WorkspaceBillingDto
        {
            SubscriptionTier = workspace.SubscriptionTier.ToString(),
            SubscriptionEndDate = workspace.SubscriptionEndDate,
            IsEarlyBirdPromo = workspace.IsEarlyBirdPromo,
            MonthlyDocumentLimit = PricingCatalog.MonthlyDocumentLimit(workspace.SubscriptionTier),
            DocumentsIssuedThisMonth = documentsIssuedThisMonth,
            PendingGCashSubmission = pendingSubmission is null
                ? null
                : new PendingGCashSubmissionDto
                {
                    GCashReferenceNumber = pendingSubmission.GCashReferenceNumber,
                    Tier = pendingSubmission.Tier.ToString(),
                    SubmittedAtUtc = pendingSubmission.SubmittedAtUtc
                }
        };
    }
}
