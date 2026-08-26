using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Admin;
using SalesDesk.Domain.Users;

namespace SalesDesk.Api.Controllers.Admin;

public sealed record SetWorkspaceStatusRequest(bool IsActive);

public sealed record SetWorkspaceQuotaRequest(int? DocumentQuota);

/// <summary>Workspace directory — TASK-017 AC3.</summary>
[ApiController]
[Route("api/admin/workspaces")]
[Authorize(Roles = nameof(Role.SystemAdmin))]
public sealed class AdminWorkspacesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WorkspaceSummaryDto>>> GetAll([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWorkspacesQuery(search), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<WorkspaceSummaryDto>> SetStatus(Guid id, [FromBody] SetWorkspaceStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SetWorkspaceStatusCommand(id, request.IsActive), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/quota")]
    public async Task<ActionResult<WorkspaceSummaryDto>> SetQuota(Guid id, [FromBody] SetWorkspaceQuotaRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SetWorkspaceQuotaCommand(id, request.DocumentQuota), cancellationToken);
        return Ok(result);
    }
}
