using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Documents;
using SalesDesk.Application.Recurring;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Api.Controllers;

public sealed record CreateRecurringScheduleRequest(
    Guid CustomerId,
    Guid TemplateId,
    DocumentType Type,
    DateOnly StartDate,
    RecurrenceInterval Interval,
    int DueDateOffsetDays,
    bool AutoDispatch,
    List<CreateDocumentLineItemRequest> LineItems,
    string? Currency = null,
    string? ClientCountry = null);

/// <summary>VERSION-2 roadmap item 3: "repeat this document every [interval]" retainer schedules.</summary>
[ApiController]
[Route("api/recurring-schedules")]
public sealed class RecurringSchedulesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RecurringScheduleDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRecurringSchedulesQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost]
    public async Task<ActionResult<RecurringScheduleDto>> Create([FromBody] CreateRecurringScheduleRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateRecurringScheduleCommand(
            request.CustomerId, request.TemplateId, request.Type, request.StartDate, request.Interval,
            request.DueDateOffsetDays, request.AutoDispatch, request.LineItems, request.Currency, request.ClientCountry);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<RecurringScheduleDto>> Pause(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PauseRecurringScheduleCommand(id), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<RecurringScheduleDto>> Resume(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResumeRecurringScheduleCommand(id), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRecurringScheduleCommand(id), cancellationToken);
        return NoContent();
    }
}
