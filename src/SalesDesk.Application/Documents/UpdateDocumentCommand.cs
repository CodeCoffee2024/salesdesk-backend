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
/// fixed once a document exists — this only covers what a legitimate "edit a draft"
/// flow needs: reschedule, re-template, re-price the line items, or change status.
/// </summary>
public sealed record UpdateDocumentCommand(
    Guid Id,
    Guid TemplateId,
    DateOnly DueDate,
    DocumentStatus Status,
    List<CreateDocumentLineItemRequest> LineItems) : IRequest<DocumentDto>;

public sealed class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(c => c.TemplateId).NotEmpty();
        RuleForEach(c => c.LineItems).SetValidator(new CreateDocumentLineItemRequestValidator());
    }
}

public sealed class UpdateDocumentCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
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

        if (document.TemplateId != request.TemplateId
            && !await context.Templates.AnyAsync(t => t.Id == request.TemplateId && t.WorkspaceId == workspaceId, cancellationToken))
        {
            throw new NotFoundException(nameof(Template), request.TemplateId);
        }

        document.ChangeTemplate(request.TemplateId);
        document.Reschedule(request.DueDate);
        document.ChangeStatus(request.Status);
        document.ReplaceLineItems(request.LineItems.Select(item =>
            new NewLineItem(item.Description, item.Quantity, item.UnitPrice, item.ProductId)));

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

        return mapper.Map<DocumentDto>(updated);
    }
}
