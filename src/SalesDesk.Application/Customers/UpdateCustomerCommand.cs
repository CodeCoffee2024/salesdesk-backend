using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Application.Customers;

public sealed record UpdateCustomerCommand(Guid Id, string Name, string Company, string Email, string? Phone, string? Country = null) : IRequest<CustomerDto>;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Company).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Country).Matches("^[A-Za-z]{2}$").WithMessage("Country must be a 2-letter ISO 3166-1 alpha-2 code.").When(c => c.Country is not null);
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

        customer.UpdateDetails(request.Name, request.Company, request.Email, request.Phone, request.Country);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<CustomerDto>(customer);
    }
}
