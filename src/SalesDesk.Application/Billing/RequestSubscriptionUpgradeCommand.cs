using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Auth;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Billing;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Billing;

public sealed record UpgradeRequestConfirmationDto(string Tier, string BillingCycle, DateTimeOffset RequestedAtUtc);

/// <summary>
/// The fallback upgrade path shown wherever a workspace can't pay through a
/// configured method — no card/PayPal gateway exists yet (IPaymentGatewayService
/// is a stub) and GCash only applies to PH workspaces. Records the request and
/// emails the platform admin a one-click approval link; nothing is charged here.
/// </summary>
public sealed record RequestSubscriptionUpgradeCommand(string Tier, string BillingCycle, string? Note) : IRequest<UpgradeRequestConfirmationDto>;

public sealed class RequestSubscriptionUpgradeCommandValidator : AbstractValidator<RequestSubscriptionUpgradeCommand>
{
    public RequestSubscriptionUpgradeCommandValidator()
    {
        RuleFor(c => c.Tier).Must(t => t is "Pro" or "Studio").WithMessage("Tier must be Pro or Studio.");
        RuleFor(c => c.BillingCycle).Must(c => c is "Monthly" or "Annual").WithMessage("BillingCycle must be Monthly or Annual.");
        RuleFor(c => c.Note).MaximumLength(2000);
    }
}

public sealed class RequestSubscriptionUpgradeCommandHandler(
    IApplicationDbContext context, ICurrentUserService currentUser, IDateTime dateTime, IEmailSender emailSender, IPublicLinkBuilder linkBuilder, IBillingSettings billingSettings)
    : IRequestHandler<RequestSubscriptionUpgradeCommand, UpgradeRequestConfirmationDto>
{
    public async Task<UpgradeRequestConfirmationDto> Handle(RequestSubscriptionUpgradeCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var workspace = await context.Workspaces.SingleAsync(w => w.Id == workspaceId, cancellationToken);

        var tier = Enum.Parse<SubscriptionTier>(request.Tier);
        var now = dateTime.UtcNow;
        var (rawToken, tokenHash) = SecureTokens.Generate();

        var upgradeRequest = new SubscriptionUpgradeRequest(workspaceId, tier, request.BillingCycle, request.Note, tokenHash, now);

        context.SubscriptionUpgradeRequests.Add(upgradeRequest);
        await context.SaveChangesAsync(cancellationToken);

        await NotifyAdminAsync(workspace, upgradeRequest, rawToken, cancellationToken);

        return new UpgradeRequestConfirmationDto(upgradeRequest.Tier.ToString(), upgradeRequest.BillingCycle, upgradeRequest.RequestedAtUtc);
    }

    private async Task NotifyAdminAsync(Workspace workspace, SubscriptionUpgradeRequest upgradeRequest, string rawToken, CancellationToken cancellationToken)
    {
        var adminEmail = billingSettings.AdminNotificationEmail;
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            // No destination configured yet — the request itself is still saved
            // and the workspace still gets its pending state; there's just nobody
            // to page about it until this is set.
            return;
        }

        var approveUrl = linkBuilder.BuildApproveUpgradeRequestUrl(rawToken);
        var noteHtml = string.IsNullOrWhiteSpace(upgradeRequest.Note)
            ? ""
            : $"""<p><strong>Note from the workspace:</strong> "{System.Net.WebUtility.HtmlEncode(upgradeRequest.Note)}"</p>""";

        var body = $"""
            <p>A workspace asked to be upgraded manually — no payment method was available to them.</p>
            <ul>
              <li><strong>Workspace:</strong> {workspace.Name} ({workspace.Email})</li>
              <li><strong>Country:</strong> {workspace.Country}</li>
              <li><strong>Requested tier:</strong> {upgradeRequest.Tier} ({upgradeRequest.BillingCycle})</li>
            </ul>
            {noteHtml}
            <p>Arrange payment with them directly (invoice, bank transfer, etc.) before approving — this only activates the tier, it doesn't collect any payment itself.</p>
            {EmailBranding.CtaButton("Approve upgrade", approveUrl)}
            """;

        await emailSender.SendAsync(
            new EmailMessage(adminEmail, Cc: null, $"Upgrade request — {workspace.Name} ({upgradeRequest.Tier})", EmailBranding.System(body)),
            cancellationToken);
    }
}
