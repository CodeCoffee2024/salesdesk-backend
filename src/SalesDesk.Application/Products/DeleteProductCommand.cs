using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Products;

namespace SalesDesk.Application.Products;

public sealed record DeleteProductCommand(Guid Id) : IRequest;

public sealed class DeleteProductCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser) : IRequestHandler<DeleteProductCommand>
{
    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        // Any line item referencing this product has its ProductId set to null at
        // the database level instead of being blocked (see
        // DocumentLineItemConfiguration) — deleting a product never fails here.
        context.Products.Remove(product);
        await context.SaveChangesAsync(cancellationToken);
    }
}
