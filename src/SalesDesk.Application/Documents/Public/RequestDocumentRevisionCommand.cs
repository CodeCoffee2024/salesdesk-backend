using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Notifications;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents.Public;

/// <summary>Backs POST /api/public/documents/{token}/request-revision (TASK-027) — the client's "Request changes" action from the public document view.</summary>
public sealed record RequestDocumentRevisionCommand(Guid Token, string Feedback) : IRequest<PublicDocumentDto>;

public sealed class RequestDocumentRevisionCommandValidator : AbstractValidator<RequestDocumentRevisionCommand>
{
    public RequestDocumentRevisionCommandValidator()
    {
        RuleFor(c => c.Feedback).NotEmpty().MaximumLength(2000);
    }
}

public sealed class RequestDocumentRevisionCommandHandler(
    IApplicationDbContext context, IDateTime dateTime, WorkspacePushNotifier pushNotifier, IPublicLinkBuilder linkBuilder)
    : IRequestHandler<RequestDocumentRevisionCommand, PublicDocumentDto>
{
    public async Task<PublicDocumentDto> Handle(RequestDocumentRevisionCommand request, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.LineItems)
            .Include(d => d.Signature)
            .FirstOrDefaultAsync(d => d.PublicToken == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Token);

        document.RequestRevision(request.Feedback, dateTime.UtcNow.UtcDateTime);
        await context.SaveChangesAsync(cancellationToken);

        var workspace = await context.Workspaces.FirstAsync(w => w.Id == document.WorkspaceId, cancellationToken);

        var customerName = document.Customer?.Name ?? "A client";
        var preview = request.Feedback.Length > 120 ? request.Feedback[..120] + "…" : request.Feedback;
        await pushNotifier.NotifyWorkspaceAsync(
            document.WorkspaceId,
            title: $"Changes requested on {document.DocumentNumber}",
            body: $"{customerName}: \"{preview}\"",
            url: linkBuilder.BuildDocumentPreviewUrl(document.Id),
            cancellationToken);

        return PublicDocumentMapper.ToDto(document, workspace.Name, workspace.LogoUrl);
    }
}
