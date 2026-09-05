using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Workspaces;

namespace SalesDesk.Api.Controllers;

public sealed record UpdateWorkspaceProfileRequest(string Name, string Email, string? Tagline, string? Address, string? LogoUrl, string Country, string DefaultCurrency, string TimeZoneId);

[ApiController]
[Route("api/workspace/profile")]
public sealed class WorkspaceProfileController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WorkspaceProfileDto>> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWorkspaceProfileQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPut]
    public async Task<ActionResult<WorkspaceProfileDto>> Update([FromBody] UpdateWorkspaceProfileRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateWorkspaceProfileCommand(request.Name, request.Email, request.Tagline, request.Address, request.LogoUrl, request.Country, request.DefaultCurrency, request.TimeZoneId);
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
