using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using SalesDesk.Application.Admin;
using SalesDesk.Domain.Users;

namespace SalesDesk.Api.Controllers.Admin;

/// <summary>
/// TASK-035: on-demand demo-data provisioning for QA, marketing screenshots, and
/// live demos. Deliberately its own tiny controller (not folded into
/// AdminWorkspacesController) so the Production Environment Isolation Guardrail is
/// impossible to miss on a future read of this file: the environment check lives
/// right here, in the one layer (Api) that actually knows what environment this
/// is. SeedDemoWorkspaceCommandHandler itself has no opinion on hosting environment.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(Role.SystemAdmin))]
public sealed class AdminSeedController(ISender sender, IHostEnvironment environment) : ControllerBase
{
    [HttpPost("seed-demo")]
    public async Task<ActionResult<SeedDemoWorkspaceResultDto>> SeedDemo(CancellationToken cancellationToken)
    {
        if (environment.IsProduction())
        {
            return Problem(
                title: "Demo seeding is disabled in production",
                detail: "POST /api/admin/seed-demo only runs outside a Production environment, to prevent an accidental reset of a live workspace's data.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await sender.Send(new SeedDemoWorkspaceCommand(), cancellationToken);
        return Ok(result);
    }
}
