using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Application.Customers;

public sealed record UpdateCustomerCommand(Guid Id, string Name, string Company, string Email, string? Phone) : IRequest<CustomerDto>;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Company).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}

public sealed class UpdateCustomerCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<UpdateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        customer.UpdateDetails(request.Name, request.Company, request.Email, request.Phone);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<CustomerDto>(customer);
    }
}
