using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Customers;

namespace SalesDesk.Api.Controllers;

public sealed record CreateCustomerRequest(string Name, string Company, string Email, string? Phone);

public sealed record UpdateCustomerRequest(string Name, string Company, string Email, string? Phone);

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCustomersQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(request.Name, request.Company, request.Email, request.Phone);
        var result = await sender.Send(command, cancellationToken);

        return Created($"/api/customers/{result.Id}", result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateCustomerCommand(id, request.Name, request.Company, request.Email, request.Phone);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = Policies.CanDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteCustomerCommand(id), cancellationToken);
        return NoContent();
    }
}
