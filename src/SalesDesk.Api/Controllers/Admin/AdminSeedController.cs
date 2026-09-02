using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
///
/// Production is blocked by default, but the account owner can deliberately lift
/// that (e.g. to seed a demo workspace for marketing screenshots on the live site)
/// by setting Seed:AllowInProduction=true on the deployment platform itself, never
/// by an agent or contributor editing this file to remove the check. That keeps the
/// override in the one place that actually controls production: the platform's own
/// secret/env store, not source code.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(Role.SystemAdmin))]
public sealed class AdminSeedController(ISender sender, IHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    [HttpPost("seed-demo")]
    public async Task<ActionResult<SeedDemoWorkspaceResultDto>> SeedDemo(CancellationToken cancellationToken)
    {
        var allowedInProduction = configuration.GetValue<bool>("Seed:AllowInProduction");
        if (environment.IsProduction() && !allowedInProduction)
        {
            return Problem(
                title: "Demo seeding is disabled in production",
                detail: "POST /api/admin/seed-demo only runs outside a Production environment by default, to prevent an accidental reset of a live workspace's data. " +
                        "Set Seed:AllowInProduction=true on the deployment platform to lift this deliberately.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await sender.Send(new SeedDemoWorkspaceCommand(), cancellationToken);
        return Ok(result);
    }
}
