using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Templates;

public sealed record DeleteTemplateCommand(Guid Id) : IRequest;

public sealed class DeleteTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser) : IRequestHandler<DeleteTemplateCommand>
{
    public async Task Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var template = await context.Templates
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Template), request.Id);

        // Restricted at the database level (see DocumentConfiguration) if any
        // document still references this template — SaveChangesAsync then throws
        // DbUpdateException, which the API layer maps to 409 Conflict.
        context.Templates.Remove(template);
        await context.SaveChangesAsync(cancellationToken);
    }
}
