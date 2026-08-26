using AutoMapper;
using FluentValidation;
using MediatR;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Application.Customers;

public sealed record CreateCustomerCommand(string Name, string Company, string Email, string? Phone) : IRequest<CustomerDto>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Company).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}

public sealed class CreateCustomerCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = new Customer(currentUser.RequireWorkspaceId(), request.Name, request.Company, request.Email, request.Phone);

        context.Customers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<CustomerDto>(customer);
    }
}
