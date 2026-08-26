using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Admin;
using SalesDesk.Domain.Users;

namespace SalesDesk.Api.Controllers.Admin;

/// <summary>System Admin Console platform metrics — TASK-017 AC2.</summary>
[ApiController]
[Route("api/admin/metrics")]
[Authorize(Roles = nameof(Role.SystemAdmin))]
public sealed class AdminMetricsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformMetricsDto>> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPlatformMetricsQuery(), cancellationToken);
        return Ok(result);
    }
}
