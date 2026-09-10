using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Notifications;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents;

/// <summary>
/// Driven by the Stripe webhook (TASK-042) once it verifies a Checkout session
/// for an invoice actually completed — never called directly from a client
/// request, so it's keyed by the internal Document Id (from the session's
/// client_reference_id/metadata), not the public token. Idempotent: Stripe
/// retries webhook deliveries automatically, so a document that's already Paid
/// short-circuits here before touching the database or sending a duplicate
/// notification — Document.RecordPayment repeats the same check as a
/// defense-in-depth measure, not the only guard against a double-send.
/// </summary>
public sealed record RecordDocumentPaymentCommand(
    Guid DocumentId, decimal AmountPaid, string Currency, string ProviderReference, DateTime PaidAtUtc) : IRequest;

public sealed class RecordDocumentPaymentCommandHandler(
    IApplicationDbContext context, WorkspacePushNotifier pushNotifier, IPublicLinkBuilder linkBuilder, IEmailSender emailSender)
    : IRequestHandler<RecordDocumentPaymentCommand>
{
    public async Task Handle(RecordDocumentPaymentCommand request, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(d => d.Activities)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.DocumentId);

        if (document.PaymentStatus == PaymentStatus.Paid)
        {
            return;
        }

        document.RecordPayment(request.AmountPaid, request.PaidAtUtc, request.ProviderReference);
        context.DocumentActivities.Add(document.Activities.Last());
        await context.SaveChangesAsync(cancellationToken);

        var workspace = await context.Workspaces.FirstAsync(w => w.Id == document.WorkspaceId, cancellationToken);
        var previewUrl = linkBuilder.BuildDocumentPreviewUrl(document.Id);
        var formattedAmount = CurrencyFormatter.Format(request.AmountPaid, request.Currency);

        await pushNotifier.NotifyWorkspaceAsync(
            document.WorkspaceId,
            title: $"{document.DocumentNumber} was paid",
            body: $"Payment of {formattedAmount} received online.",
            url: previewUrl,
            cancellationToken);

        // TASK-034, Template 3 (Activity & Status Update): notifies the workspace
        // owner that money actually arrived, the same shape SignDocumentCommandHandler
        // uses for its own client-triggered event.
        var emailBody = $"""
            <p><strong>{formattedAmount}</strong> was just paid online for invoice <strong>{document.DocumentNumber}</strong>.</p>
            {EmailBranding.CtaButton("View document", previewUrl)}
            {DocumentActivityEmailFormatter.BuildTimelineHtml(document.Activities, forClient: false, workspace.TimeZoneId)}
            """;
        await emailSender.SendAsync(
            new EmailMessage(workspace.Email, Cc: null, $"{document.DocumentNumber} was paid",
                EmailBranding.Workspace(workspace.Name, workspace.LogoUrl, workspace.Tagline, workspace.Address, workspace.Email, emailBody)),
            cancellationToken);
    }
}
