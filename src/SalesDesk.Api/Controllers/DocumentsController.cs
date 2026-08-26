using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Api.Controllers;

public sealed record CreateDocumentRequest(
    DocumentType Type,
    Guid CustomerId,
    Guid TemplateId,
    DateOnly DueDate,
    List<CreateDocumentLineItemRequest> LineItems);

public sealed record UpdateDocumentRequest(
    Guid TemplateId,
    DateOnly DueDate,
    DocumentStatus Status,
    List<CreateDocumentLineItemRequest> LineItems);

public sealed record UpdateDocumentStatusRequest(DocumentStatus Status);

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(ISender sender) : ControllerBase
{
    /// <summary>GET /api/documents?type=all|quote|invoice&amp;status=...&amp;search=...</summary>
    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetAll(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDocumentsQuery(type, status, search), cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/documents/{id} — dedicated preview fetch, independent of the list above.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDocumentByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost]
    public async Task<ActionResult<DocumentDto>> Create([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateDocumentCommand(request.Type, request.CustomerId, request.TemplateId, request.DueDate, request.LineItems);
        var result = await sender.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> Update(Guid id, [FromBody] UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateDocumentCommand(id, request.TemplateId, request.DueDate, request.Status, request.LineItems);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    [Authorize(Policy = Policies.CanDelete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDocumentCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>PATCH /api/documents/{id}/status — a narrow update for lifecycle actions (Mark as Sent/Paid/Accepted, etc).</summary>
    [Authorize(Policy = Policies.CanManage)]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<DocumentDto>> UpdateStatus(Guid id, [FromBody] UpdateDocumentStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateDocumentStatusCommand(id, request.Status), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.CanManage)]
    [HttpPost("{id:guid}/convert-to-invoice")]
    public async Task<ActionResult<DocumentDto>> ConvertToInvoice(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ConvertQuoteToInvoiceCommand(id), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
