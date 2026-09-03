using System.Text.RegularExpressions;
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

public sealed record GCashSubmissionConfirmationDto(string GCashReferenceNumber, DateTimeOffset SubmittedAtUtc);

/// <summary>TASK-039: the proof-of-payment form on /settings/billing's "Pay via GCash" option.</summary>
public sealed record SubmitGCashPaymentCommand(
    string Tier,
    string BillingCycle,
    string GCashReferenceNumber,
    string SenderName,
    string SenderMobileNumber,
    /// <summary>A "data:image/jpeg;base64,..." or "data:image/png;base64,..." string, or null — optional per the task spec.</summary>
    string? ScreenshotDataUrl) : IRequest<GCashSubmissionConfirmationDto>;

public sealed class SubmitGCashPaymentCommandValidator : AbstractValidator<SubmitGCashPaymentCommand>
{
    // ~2MB of image data after base64's ~33% size overhead — generous for a phone
    // screenshot of a GCash receipt while still keeping the row (and the admin
    // notification email it gets embedded into) a sane size.
    private const int MaxScreenshotDataUrlLength = 2_800_000;

    public SubmitGCashPaymentCommandValidator()
    {
        RuleFor(c => c.Tier).Must(t => t is "Pro" or "Studio").WithMessage("Tier must be Pro or Studio.");
        RuleFor(c => c.BillingCycle).Must(c => c is "Monthly" or "Annual").WithMessage("BillingCycle must be Monthly or Annual.");
        RuleFor(c => c.GCashReferenceNumber).Matches("^\\d{13}$").WithMessage("GCash reference number must be exactly 13 digits.");
        RuleFor(c => c.SenderName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SenderMobileNumber).Matches("^(\\+63|0)9\\d{9}$").WithMessage("Enter a valid PH mobile number, e.g. 09171234567.");
        RuleFor(c => c.ScreenshotDataUrl)
            .Must(url => Regex.IsMatch(url!, "^data:image/(png|jpe?g);base64,"))
            .WithMessage("Screenshot must be a PNG or JPEG image.")
            .When(c => c.ScreenshotDataUrl is not null);
        RuleFor(c => c.ScreenshotDataUrl)
            .Must(url => url!.Length <= MaxScreenshotDataUrlLength)
            .WithMessage("Screenshot is too large — please use a smaller image.")
            .When(c => c.ScreenshotDataUrl is not null);
    }
}

public sealed class SubmitGCashPaymentCommandHandler(
    IApplicationDbContext context, ICurrentUserService currentUser, IDateTime dateTime, IEmailSender emailSender, IPublicLinkBuilder linkBuilder, IBillingSettings billingSettings)
    : IRequestHandler<SubmitGCashPaymentCommand, GCashSubmissionConfirmationDto>
{
    public async Task<GCashSubmissionConfirmationDto> Handle(SubmitGCashPaymentCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var workspace = await context.Workspaces.SingleAsync(w => w.Id == workspaceId, cancellationToken);

        // GCash only makes sense for Philippine subscribers — the pricing catalog's
        // Global side doesn't even quote a PHP amount to charge.
        if (!string.Equals(workspace.Country, "PH", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("GCash payment is only available for Philippines-based workspaces.");
        }

        var tier = Enum.Parse<SubscriptionTier>(request.Tier);
        var pricedTier = PricingCatalog.ForCountry("PH").Tiers.Single(t => t.Tier == request.Tier);
        var amount = request.BillingCycle == "Annual" ? pricedTier.AnnualPrice : pricedTier.MonthlyPrice;

        var now = dateTime.UtcNow;
        var (rawToken, tokenHash) = SecureTokens.Generate();

        var submission = new GCashPaymentSubmission(
            workspaceId, tier, request.BillingCycle, amount, request.GCashReferenceNumber,
            request.SenderName, request.SenderMobileNumber, request.ScreenshotDataUrl, tokenHash, now);

        context.GCashPaymentSubmissions.Add(submission);
        await context.SaveChangesAsync(cancellationToken);

        await NotifyAdminAsync(workspace, submission, rawToken, cancellationToken);

        return new GCashSubmissionConfirmationDto(submission.GCashReferenceNumber, submission.SubmittedAtUtc);
    }

    private async Task NotifyAdminAsync(Workspace workspace, GCashPaymentSubmission submission, string rawToken, CancellationToken cancellationToken)
    {
        var adminEmail = billingSettings.AdminNotificationEmail;
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            // No destination configured yet — the submission itself is still
            // saved and the workspace still gets its pending-verification state;
            // there's just nobody to page about it until this is set.
            return;
        }

        var approveUrl = linkBuilder.BuildApproveGCashSubscriptionUrl(rawToken);

        var screenshotHtml = submission.ScreenshotDataUrl is not null
            ? $"""<p><img src="{submission.ScreenshotDataUrl}" alt="Payment screenshot" style="max-width:100%;border-radius:8px;" /></p>"""
            : "<p><em>No screenshot attached.</em></p>";

        var body = $"""
            <p>A workspace submitted a GCash payment claim for a paid upgrade.</p>
            <ul>
              <li><strong>Workspace:</strong> {workspace.Name} ({workspace.Email})</li>
              <li><strong>Requested tier:</strong> {submission.Tier} ({submission.BillingCycle})</li>
              <li><strong>Amount claimed:</strong> ₱{submission.AmountPhp:N2}</li>
              <li><strong>GCash reference number:</strong> {submission.GCashReferenceNumber}</li>
              <li><strong>Sender name:</strong> {submission.SenderName}</li>
              <li><strong>Sender mobile:</strong> {submission.SenderMobileNumber}</li>
            </ul>
            {screenshotHtml}
            <p>Check this reference number against the platform's own GCash app before approving.</p>
            {EmailBranding.CtaButton("Approve subscription", approveUrl)}
            """;

        await emailSender.SendAsync(
            new EmailMessage(adminEmail, Cc: null, $"GCash payment claim — {workspace.Name} ({submission.Tier})", EmailBranding.System(body)),
            cancellationToken);
    }
}
