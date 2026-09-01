using MediatR;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Workspaces;

namespace SalesDesk.Api.Controllers;

/// <summary>TASK-031: read-only for now — there's no paid-upgrade flow yet, just the current workspace's subscription tier and early-bird promo status for /settings/billing.</summary>
[ApiController]
[Route("api/workspace/billing")]
public sealed class BillingController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WorkspaceBillingDto>> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWorkspaceBillingQuery(), cancellationToken);
        return Ok(result);
    }
}
