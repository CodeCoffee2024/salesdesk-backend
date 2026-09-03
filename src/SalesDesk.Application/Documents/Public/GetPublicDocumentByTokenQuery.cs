using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Notifications;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents.Public;

/// <summary>Backs GET /api/public/documents/{token} — the anonymous, client-facing document view (TASK-023/024).</summary>
public sealed record GetPublicDocumentByTokenQuery(Guid Token) : IRequest<PublicDocumentDto>;

public sealed class GetPublicDocumentByTokenQueryHandler(
    IApplicationDbContext context, WorkspacePushNotifier pushNotifier, IPublicLinkBuilder linkBuilder, IDateTime dateTime, IEmailSender emailSender)
    : IRequestHandler<GetPublicDocumentByTokenQuery, PublicDocumentDto>
{
    public async Task<PublicDocumentDto> Handle(GetPublicDocumentByTokenQuery request, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.LineItems)
            .Include(d => d.Signature)
            .FirstOrDefaultAsync(d => d.PublicToken == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Token);

        var workspace = await context.Workspaces
            .FirstAsync(w => w.Id == document.WorkspaceId, cancellationToken);

        // TASK-027: notify once, the first time a client opens the link — not on
        // every repeat view (a refresh, or the client reopening it later).
        if (document.RecordFirstView(dateTime.UtcNow.UtcDateTime))
        {
            await context.SaveChangesAsync(cancellationToken);

            var customerName = document.Customer?.Name ?? "A client";
            var previewUrl = linkBuilder.BuildDocumentPreviewUrl(document.Id);

            await pushNotifier.NotifyWorkspaceAsync(
                document.WorkspaceId,
                title: $"{document.DocumentNumber} was viewed",
                body: $"{customerName} just opened your {document.Type.ToString().ToLowerInvariant()}.",
                url: previewUrl,
                cancellationToken);

            // TASK-034, Template 3 (Activity & Status Update) — same pattern as
            // RequestDocumentRevisionCommand/SignDocumentCommand's matching sends:
            // the push notification above is easy to miss if the owner isn't
            // looking at their device, so the first-view moment also gets an email.
            var emailBody = $"""
                <p><strong>{customerName}</strong> just opened {document.Type.ToString().ToLowerInvariant()} <strong>{document.DocumentNumber}</strong>.</p>
                {EmailBranding.CtaButton("View document", previewUrl)}
                """;
            await emailSender.SendAsync(
                new EmailMessage(workspace.Email, Cc: null, $"{document.DocumentNumber} was viewed",
                    EmailBranding.Workspace(workspace.Name, workspace.LogoUrl, workspace.Tagline, workspace.Address, workspace.Email, emailBody)),
                cancellationToken);
        }

        return PublicDocumentMapper.ToDto(document, workspace.Name, workspace.LogoUrl);
    }
}
