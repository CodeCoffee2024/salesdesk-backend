using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents;

public sealed record DeleteDocumentCommand(Guid Id) : IRequest;

public sealed class DeleteDocumentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser) : IRequestHandler<DeleteDocumentCommand>
{
    public async Task Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var document = await context.Documents
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Id);

        // Line items cascade-delete at the database level (see DocumentConfiguration).
        context.Documents.Remove(document);
        await context.SaveChangesAsync(cancellationToken);
    }
}
