using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Documents;

/// <summary>
/// Full (PUT) update. CustomerId, Type, DocumentNumber and IssueDate are treated as
/// fixed once a document exists — this only covers what a legitimate "edit" flow
/// needs: reschedule, re-template, re-price the line items, or dispatch to the
/// client. Status is not a free-form input here (it was never actually used as one —
/// the frontend always echoed the document's current status) — Document.EnsureEditable
/// governs when this is even allowed to run, and Dispatch below is the only way this
/// command moves the status forward, to Sent (TASK-037).
/// </summary>
/// <param name="Dispatch">"Save &amp; Send to Client" on a Draft, or "Save &amp; Re-Send Revision" on a RevisionRequested document — both send the client the same "you've been sent a quote/invoice" notification and move the status to Sent.</param>
public sealed record UpdateDocumentCommand(
    Guid Id,
    Guid TemplateId,
    DateOnly DueDate,
    List<CreateDocumentLineItemRequest> LineItems,
    string? Currency = null,
    string? ClientCountry = null,
    bool Dispatch = false) : IRequest<DocumentDto>;

public sealed class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(c => c.TemplateId).NotEmpty();
        RuleForEach(c => c.LineItems).SetValidator(new CreateDocumentLineItemRequestValidator());

        // Null means "leave the document's current currency/country untouched" —
        // see UpdateDocumentCommandHandler (TASK-029).
        RuleFor(c => c.Currency).Matches("^[A-Za-z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code.").When(c => c.Currency is not null);
        RuleFor(c => c.ClientCountry).Matches("^[A-Za-z]{2}$").WithMessage("Client country must be a 2-letter ISO 3166-1 alpha-2 code.").When(c => c.ClientCountry is not null);
    }
}

public sealed class UpdateDocumentCommandHandler(
    IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser, IDateTime dateTime, IEmailSender emailSender, IPublicLinkBuilder linkBuilder)
    : IRequestHandler<UpdateDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UpdateDocumentCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();

        var document = await context.Documents
            .Include(d => d.LineItems)
            .Include(d => d.Signature)
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Id);

        // TASK-037 guardrail: a Sent/Accepted/Paid document's content can't change
        // out from under a client who's already seen it. Checked up front so a
        // request against a locked-for-editing document fails clean (409) before
        // any partial mutation, not partway through the calls below (which would
        // also throw the same way, just less predictably).
        document.EnsureEditable();

        if (document.TemplateId != request.TemplateId
            && !await context.Templates.AnyAsync(t => t.Id == request.TemplateId && t.WorkspaceId == workspaceId, cancellationToken))
        {
            throw new NotFoundException(nameof(Template), request.TemplateId);
        }

        document.ChangeTemplate(request.TemplateId);
        document.Reschedule(request.DueDate);
        document.ChangeCurrency(request.Currency ?? document.Currency, request.ClientCountry ?? document.ClientCountry);
        document.ReplaceLineItems(request.LineItems.Select(item =>
            new NewLineItem(item.Description, item.Quantity, item.UnitPrice, item.ProductId)));

        if (request.Dispatch)
        {
            document.Dispatch(dateTime.UtcNow.UtcDateTime);
        }

        // DocumentLineItem.Id is a client-generated Guid, never left at its CLR
        // default — so when a brand-new line item only reaches the tracker via
        // collection-navigation fixup on an already-tracked (Unchanged) Document,
        // EF Core's DetectChanges can't tell it apart from an existing row and
        // defaults it to Modified, which issues an UPDATE for a row that was never
        // inserted. Explicitly re-adding the whole (freshly-replaced) set forces
        // Added state for anything not already tracked as Unchanged.
        context.DocumentLineItems.AddRange(document.LineItems);

        await context.SaveChangesAsync(cancellationToken);

        var updated = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.Template)
            .Include(d => d.LineItems)
            .FirstAsync(d => d.Id == document.Id, cancellationToken);

        if (request.Dispatch)
        {
            await DocumentDispatchNotifier.NotifyAsync(context, emailSender, linkBuilder, updated, workspaceId, cancellationToken);
        }

        return mapper.Map<DocumentDto>(updated);
    }
}
