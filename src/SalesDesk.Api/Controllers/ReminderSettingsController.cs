using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Reminders;

namespace SalesDesk.Api.Controllers;

public sealed record SaveReminderSettingsRequest(
    bool IsEnabled,
    bool QuoteFollowUpEnabled,
    bool InvoiceDueWarningEnabled,
    bool OverdueNoticesEnabled,
    string? CcEmail);

[ApiController]
[Route("api/settings/reminders")]
public sealed class ReminderSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReminderSettingsDto>> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetReminderSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPut]
    public async Task<ActionResult<ReminderSettingsDto>> Save([FromBody] SaveReminderSettingsRequest request, CancellationToken cancellationToken)
    {
        var command = new SaveReminderSettingsCommand(
            request.IsEnabled, request.QuoteFollowUpEnabled, request.InvoiceDueWarningEnabled, request.OverdueNoticesEnabled, request.CcEmail);
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
