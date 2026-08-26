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

public sealed class UpdateDocumentStatusCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<UpdateDocumentStatusCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UpdateDocumentStatusCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var document = await context.Documents
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Id);

        document.ChangeStatus(request.Status);
        await context.SaveChangesAsync(cancellationToken);

        var updated = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.Template)
            .Include(d => d.LineItems)
            .FirstAsync(d => d.Id == document.Id, cancellationToken);

        return mapper.Map<DocumentDto>(updated);
    }
}
