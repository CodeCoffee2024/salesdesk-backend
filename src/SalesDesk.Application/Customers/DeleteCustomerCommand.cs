using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Application.Customers;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest;

public sealed class DeleteCustomerCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser) : IRequestHandler<DeleteCustomerCommand>
{
    public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        // Restricted at the database level (see DocumentConfiguration) if any
        // document still references this customer — SaveChangesAsync then throws
        // DbUpdateException, which the API layer maps to 409 Conflict.
        context.Customers.Remove(customer);
        await context.SaveChangesAsync(cancellationToken);
    }
}
