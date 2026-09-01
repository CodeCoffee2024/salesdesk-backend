using AutoMapper;
using FluentValidation;
using MediatR;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Application.Customers;

public sealed record CreateCustomerCommand(string Name, string Company, string Email, string? Phone, string? Country = null) : IRequest<CustomerDto>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Company).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Country).Matches("^[A-Za-z]{2}$").WithMessage("Country must be a 2-letter ISO 3166-1 alpha-2 code.").When(c => c.Country is not null);
    }
}

public sealed class CreateCustomerCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = new Customer(currentUser.RequireWorkspaceId(), request.Name, request.Company, request.Email, request.Phone, request.Country);

        context.Customers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<CustomerDto>(customer);
    }
}
