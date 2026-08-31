using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Documents.Public;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Api.Controllers.Public;

public sealed record SignDocumentRequest(
    string SignerName,
    string SignerEmail,
    bool AgreedToTerms,
    SignatureType SignatureType,
    string SignatureImageDataUrl);

/// <summary>
/// The unauthenticated surface a client hits from their document link (TASK-023/024)
/// — every action here is [AllowAnonymous] and scoped by the document's
/// <see cref="Document.PublicToken"/>, never its internal Id.
/// </summary>
[ApiController]
[Route("api/public/documents")]
public sealed class PublicDocumentsController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{token:guid}")]
    public async Task<ActionResult<PublicDocumentDto>> GetByToken(Guid token, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPublicDocumentByTokenQuery(token), cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("{token:guid}/signature")]
    public async Task<ActionResult<PublicDocumentDto>> Sign(Guid token, [FromBody] SignDocumentRequest request, CancellationToken cancellationToken)
    {
        // IP/User-Agent are part of the legal audit trail (TASK-024 AC3) — captured
        // here from the request itself, never trusted from the client-supplied body.
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var command = new SignDocumentCommand(
            token, request.SignerName, request.SignerEmail, request.AgreedToTerms,
            request.SignatureType, request.SignatureImageDataUrl, ipAddress, userAgent);

        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
