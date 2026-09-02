using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser, IEmailSender emailSender, IPublicLinkBuilder linkBuilder)
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
        document.ChangeStatus(request.Status);
        await context.SaveChangesAsync(cancellationToken);

        var updated = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.Template)
            .Include(d => d.LineItems)
            .FirstAsync(d => d.Id == document.Id, cancellationToken);

        // TASK-034, Templates 1/2: only on a genuine Draft/Overdue/etc -> Sent
        // transition, not every PATCH that happens to already be Sent (e.g. an
        // unrelated field save that resubmits the current status unchanged).
        if (request.Status == DocumentStatus.Sent && previousStatus != DocumentStatus.Sent && updated.Customer is not null)
        {
            // Not every caller (e.g. some tests) seeds a Workspace row for the
            // current workspace id. See CreateDocumentCommandHandler's identical
            // fallback. No workspace to brand the email with means no email.
            var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
            if (workspace is not null)
            {
                var documentUrl = linkBuilder.BuildDocumentUrl(updated.PublicToken);
                var (subject, htmlBody) = DocumentNotificationEmailTemplates.BuildSentNotification(updated, workspace, documentUrl);

                await emailSender.SendAsync(new EmailMessage(updated.Customer.Email, Cc: null, subject, htmlBody, ReplyTo: workspace.Email), cancellationToken);
            }
        }

        return mapper.Map<DocumentDto>(updated);
    }
}
