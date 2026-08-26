using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Admin;
using SalesDesk.Application.Auth;
using SalesDesk.Domain.Users;

namespace SalesDesk.Api.Controllers.Admin;

/// <summary>Platform-wide Users directory + impersonation ("view as a user").</summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = nameof(Role.SystemAdmin))]
public sealed class AdminUsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> GetAll(
        [FromQuery] string? search, [FromQuery] Guid? workspaceId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUsersQuery(search, workspaceId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/impersonate")]
    public async Task<ActionResult<AuthResponseDto>> Impersonate(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ImpersonateUserCommand(id), cancellationToken);
        return Ok(result);
    }
}
