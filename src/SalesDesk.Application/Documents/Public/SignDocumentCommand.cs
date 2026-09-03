using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Notifications;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents.Public;

/// <summary>
/// Backs POST /api/public/documents/{token}/signature — the client's "Accept &amp;
/// sign" action (TASK-024). IpAddress/UserAgent are captured server-side from the
/// request by the controller, not trusted from the request body, since they're part
/// of the legal audit trail.
/// </summary>
public sealed record SignDocumentCommand(
    Guid Token,
    string SignerName,
    string SignerEmail,
    bool AgreedToTerms,
    SignatureType SignatureType,
    string SignatureImageDataUrl,
    string IpAddress,
    string UserAgent) : IRequest<PublicDocumentDto>;

public sealed class SignDocumentCommandValidator : AbstractValidator<SignDocumentCommand>
{
    // A drawn/rasterized PNG data URL comfortably fits in a few hundred KB; this
    // caps well above that to stop a pathological payload without being a real
    // constraint on a legitimate signature.
    private const int MaxSignatureImageLength = 2_000_000;

    public SignDocumentCommandValidator()
    {
        RuleFor(c => c.SignerName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SignerEmail).NotEmpty().EmailAddress();
        RuleFor(c => c.AgreedToTerms).Equal(true).WithMessage("You must agree to the terms to sign this document.");
        RuleFor(c => c.SignatureImageDataUrl)
            .NotEmpty()
            .MaximumLength(MaxSignatureImageLength)
            .Must(value => value.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
            .WithMessage("Signature image must be a PNG data URL.");
    }
}

public sealed class SignDocumentCommandHandler(
    IApplicationDbContext context, IDateTime dateTime, WorkspacePushNotifier pushNotifier, IPublicLinkBuilder linkBuilder, IEmailSender emailSender)
    : IRequestHandler<SignDocumentCommand, PublicDocumentDto>
{
    public async Task<PublicDocumentDto> Handle(SignDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.LineItems)
            .Include(d => d.Signature)
            .Include(d => d.Activities)
            .FirstOrDefaultAsync(d => d.PublicToken == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Token);

        var signature = document.ApplySignature(
            request.SignerName,
            request.SignerEmail,
            request.SignatureType,
            request.SignatureImageDataUrl,
            request.IpAddress,
            request.UserAgent,
            dateTime.UtcNow);

        // DocumentSignature.Id is a client-generated Guid (never left at its CLR
        // default), so reaching it only via navigation fixup on an already-tracked
        // (Unchanged) Document leaves EF Core's DetectChanges unable to tell it
        // apart from an existing row — it issues an UPDATE for a row that was
        // never inserted (0 rows affected, DbUpdateConcurrencyException). Same
        // failure mode as UpdateDocumentCommand's line-item replace; same fix.
        context.DocumentSignatures.Add(signature);
        context.DocumentActivities.Add(document.Activities.Last());

        await context.SaveChangesAsync(cancellationToken);

        var workspace = await context.Workspaces.FirstAsync(w => w.Id == document.WorkspaceId, cancellationToken);

        var previewUrl = linkBuilder.BuildDocumentPreviewUrl(document.Id);

        await pushNotifier.NotifyWorkspaceAsync(
            document.WorkspaceId,
            title: $"{document.DocumentNumber} was signed",
            body: $"{request.SignerName} just signed your {document.Type.ToString().ToLowerInvariant()}.",
            url: previewUrl,
            cancellationToken);

        // TASK-034, Template 3 (Activity & Status Update): notifies the workspace
        // owner, not the client. Still workspace-branded (it's their own business's
        // inbox), unlike the platform-only System() wrapper used for auth emails.
        var emailBody = $"""
            <p><strong>{request.SignerName}</strong> just signed {document.Type.ToString().ToLowerInvariant()} <strong>{document.DocumentNumber}</strong> ({CurrencyFormatter.Format(document.Total, document.Currency)}).</p>
            {EmailBranding.CtaButton("View document", previewUrl)}
            {DocumentActivityEmailFormatter.BuildTimelineHtml(document.Activities, forClient: false)}
            """;
        await emailSender.SendAsync(
            new EmailMessage(workspace.Email, Cc: null, $"{document.DocumentNumber} was signed",
                EmailBranding.Workspace(workspace.Name, workspace.LogoUrl, workspace.Tagline, workspace.Address, workspace.Email, emailBody)),
            cancellationToken);

        return PublicDocumentMapper.ToDto(document, workspace.Name, workspace.LogoUrl);
    }
}
