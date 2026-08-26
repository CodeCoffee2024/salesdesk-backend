using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Admin;
using SalesDesk.Domain.Users;

namespace SalesDesk.Api.Controllers.Admin;

/// <summary>Global audit log — TASK-017 AC4.</summary>
[ApiController]
[Route("api/admin/audit-log")]
[Authorize(Roles = nameof(Role.SystemAdmin))]
public sealed class AdminAuditLogController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetAuditLogQuery(search, page, pageSize), cancellationToken);
        return Ok(result);
    }
}
