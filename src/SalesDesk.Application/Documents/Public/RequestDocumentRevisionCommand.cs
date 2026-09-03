using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Email;
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
    IApplicationDbContext context, IDateTime dateTime, WorkspacePushNotifier pushNotifier, IPublicLinkBuilder linkBuilder, IEmailSender emailSender)
    : IRequestHandler<RequestDocumentRevisionCommand, PublicDocumentDto>
{
    public async Task<PublicDocumentDto> Handle(RequestDocumentRevisionCommand request, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.LineItems)
            .Include(d => d.Signature)
            .Include(d => d.Activities)
            .FirstOrDefaultAsync(d => d.PublicToken == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Token);

        document.RequestRevision(request.Feedback, dateTime.UtcNow.UtcDateTime);
        context.DocumentActivities.Add(document.Activities.Last());
        await context.SaveChangesAsync(cancellationToken);

        var workspace = await context.Workspaces.FirstAsync(w => w.Id == document.WorkspaceId, cancellationToken);

        var customerName = document.Customer?.Name ?? "A client";
        var preview = request.Feedback.Length > 120 ? request.Feedback[..120] + "…" : request.Feedback;
        var previewUrl = linkBuilder.BuildDocumentPreviewUrl(document.Id);

        await pushNotifier.NotifyWorkspaceAsync(
            document.WorkspaceId,
            title: $"Changes requested on {document.DocumentNumber}",
            body: $"{customerName}: \"{preview}\"",
            url: previewUrl,
            cancellationToken);

        // TASK-034, Template 3 (Activity & Status Update). See SignDocumentCommandHandler's matching send for why this is Workspace-, not System-, branded.
        var emailBody = $"""
            <p><strong>{customerName}</strong> requested changes on <strong>{document.DocumentNumber}</strong>:</p>
            <p style="padding:12px 16px;background:#f4f5f9;border-radius:8px;">&ldquo;{System.Net.WebUtility.HtmlEncode(request.Feedback)}&rdquo;</p>
            {EmailBranding.CtaButton("View document", previewUrl)}
            {DocumentActivityEmailFormatter.BuildTimelineHtml(document.Activities, forClient: false)}
            """;
        await emailSender.SendAsync(
            new EmailMessage(workspace.Email, Cc: null, $"Changes requested on {document.DocumentNumber}",
                EmailBranding.Workspace(workspace.Name, workspace.LogoUrl, workspace.Tagline, workspace.Address, workspace.Email, emailBody)),
            cancellationToken);

        return PublicDocumentMapper.ToDto(document, workspace.Name, workspace.LogoUrl);
    }
}
