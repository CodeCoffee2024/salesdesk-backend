using MediatR;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Dashboard;

namespace SalesDesk.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDashboardSummaryQuery(), cancellationToken);
        return Ok(result);
    }
}
