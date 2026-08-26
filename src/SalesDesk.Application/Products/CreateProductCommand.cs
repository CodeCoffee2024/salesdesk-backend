using AutoMapper;
using FluentValidation;
using MediatR;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Products;

namespace SalesDesk.Application.Products;

public sealed record CreateProductCommand(string Name, decimal Price, ProductUnit Unit, string? Description, string? Category)
    : IRequest<ProductDto>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(p => p.Name).NotEmpty();
        RuleFor(p => p.Price).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateProductCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(currentUser.RequireWorkspaceId(), request.Name, request.Price, request.Unit, request.Description, request.Category);

        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<ProductDto>(product);
    }
}
