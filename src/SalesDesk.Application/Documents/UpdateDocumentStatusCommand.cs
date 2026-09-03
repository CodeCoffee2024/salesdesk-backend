using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Email;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents;

/// <summary>
/// Backs PATCH /api/documents/{id}/status — a narrow, single-field update
/// (kept separate from the full PUT) so lifecycle actions like "Mark as Sent" don't
/// need to resend the whole document body.
/// </summary>
public sealed record UpdateDocumentStatusCommand(Guid Id, DocumentStatus Status) : IRequest<DocumentDto>;

public sealed class UpdateDocumentStatusCommandHandler(
    IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser, IEmailSender emailSender, IPublicLinkBuilder linkBuilder, IDateTime dateTime)
    : IRequestHandler<UpdateDocumentStatusCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UpdateDocumentStatusCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var document = await context.Documents
            .Include(d => d.Signature)
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Id);

        var previousStatus = document.Status;

        // "Mark as Sent" from this narrow lifecycle PATCH goes through Dispatch too
        // (TASK-037), so IsDispatched/DispatchedAt stay accurate no matter which of
        // the three dispatch entry points (create, edit, or this one) fires it.
        if (request.Status == DocumentStatus.Sent)
        {
            document.Dispatch(dateTime.UtcNow.UtcDateTime);
        }
        else
        {
            document.ChangeStatus(request.Status);
        }

        await context.SaveChangesAsync(cancellationToken);

        var updated = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.Template)
            .Include(d => d.LineItems)
            .FirstAsync(d => d.Id == document.Id, cancellationToken);

        // TASK-034, Templates 1/2: only on a genuine Draft/RevisionRequested -> Sent
        // transition, not every PATCH that happens to already be Sent (e.g. an
        // unrelated field save that resubmits the current status unchanged).
        if (request.Status == DocumentStatus.Sent && previousStatus != DocumentStatus.Sent)
        {
            await DocumentDispatchNotifier.NotifyAsync(context, emailSender, linkBuilder, updated, workspaceId, cancellationToken);
        }

        // Every other lifecycle action the workspace takes on a document (marking
        // a quote Accepted, an invoice Paid) is worth telling the client about too
        // — not just the client-initiated ones (viewing, e-signing, requesting a
        // revision) that already email back to the workspace above.
        if (request.Status is DocumentStatus.Accepted or DocumentStatus.Paid && previousStatus != request.Status)
        {
            await NotifyCustomerOfStatusChangeAsync(updated, request.Status, workspaceId, cancellationToken);
        }

        return mapper.Map<DocumentDto>(updated);
    }

    private async Task NotifyCustomerOfStatusChangeAsync(Document document, DocumentStatus status, Guid workspaceId, CancellationToken cancellationToken)
    {
        if (document.Customer is null)
        {
            return;
        }

        var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        if (workspace is null)
        {
            return;
        }

        var documentUrl = linkBuilder.BuildDocumentUrl(document.PublicToken);
        var (subject, bodyLine) = status == DocumentStatus.Paid
            ? ($"Payment received for {document.DocumentNumber}", $"<p>{workspace.Name} has marked invoice <strong>{document.DocumentNumber}</strong> ({document.Total:C}) as <strong>paid</strong>. Thanks for your business!</p>")
            : ($"{document.DocumentNumber} marked as accepted", $"<p>{workspace.Name} has marked quote <strong>{document.DocumentNumber}</strong> ({document.Total:C}) as <strong>accepted</strong>.</p>");

        var emailBody = $"""
            <p>Hi {document.Customer.Name},</p>
            {bodyLine}
            {EmailBranding.CtaButton("View document", documentUrl)}
            """;

        await emailSender.SendAsync(
            new EmailMessage(document.Customer.Email, Cc: null, subject,
                EmailBranding.Workspace(workspace.Name, workspace.LogoUrl, workspace.Tagline, workspace.Address, workspace.Email, emailBody), ReplyTo: workspace.Email),
            cancellationToken);
    }
}
