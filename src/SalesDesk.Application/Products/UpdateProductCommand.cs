using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Products;

namespace SalesDesk.Application.Products;

public sealed record UpdateProductCommand(Guid Id, string Name, decimal Price, ProductUnit Unit, string? Description, string? Category)
    : IRequest<ProductDto>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(p => p.Name).NotEmpty();
        RuleFor(p => p.Price).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<UpdateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var product = await context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        product.UpdateDetails(request.Name, request.Price, request.Unit, request.Description, request.Category);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<ProductDto>(product);
    }
}
