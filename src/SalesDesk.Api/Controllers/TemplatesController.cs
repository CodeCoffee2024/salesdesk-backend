using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Templates;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Api.Controllers;

public sealed record CreateTemplateRequest(string Name, TemplateTargetType TargetType, string? Description, string? AccentColor);

public sealed record UpdateTemplateRequest(string Name, TemplateTargetType TargetType, string? Description, string? AccentColor);

[ApiController]
[Route("api/templates")]
public sealed class TemplatesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TemplateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTemplatesQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost]
    public async Task<ActionResult<TemplateDto>> Create([FromBody] CreateTemplateRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateTemplateCommand(request.Name, request.TargetType, request.Description, request.AccentColor);
        var result = await sender.Send(command, cancellationToken);

        return Created($"/api/templates/{result.Id}", result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TemplateDto>> Update(Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateTemplateCommand(id, request.Name, request.TargetType, request.Description, request.AccentColor);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = Policies.CanDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTemplateCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost("{id:guid}/set-default")]
    public async Task<ActionResult<TemplateDto>> SetDefault(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SetDefaultTemplateCommand(id), cancellationToken);
        return Ok(result);
    }
}
