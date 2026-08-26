using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Products;
using SalesDesk.Domain.Products;

namespace SalesDesk.Api.Controllers;

public sealed record CreateProductRequest(string Name, decimal Price, ProductUnit Unit, string? Description, string? Category);

public sealed record UpdateProductRequest(string Name, decimal Price, ProductUnit Unit, string? Description, string? Category);

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProductsQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(request.Name, request.Price, request.Unit, request.Description, request.Category);
        var result = await sender.Send(command, cancellationToken);

        return Created($"/api/products/{result.Id}", result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateProductCommand(id, request.Name, request.Price, request.Unit, request.Description, request.Category);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = Policies.CanDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProductCommand(id), cancellationToken);
        return NoContent();
    }
}
