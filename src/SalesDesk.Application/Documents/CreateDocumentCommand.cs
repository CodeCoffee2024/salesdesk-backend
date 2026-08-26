using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Documents;

public sealed record CreateDocumentLineItemRequest(string Description, decimal Quantity, decimal UnitPrice, Guid? ProductId);

/// <summary>
/// Shared by every command that carries a set of line items (create and update) —
/// one place for the "Quantity &gt; 0, Price &gt;= 0, non-empty Description" rule.
/// </summary>
public sealed class CreateDocumentLineItemRequestValidator : AbstractValidator<CreateDocumentLineItemRequest>
{
    public CreateDocumentLineItemRequestValidator()
    {
        RuleFor(li => li.Description).NotEmpty();
        RuleFor(li => li.Quantity).GreaterThan(0);
        RuleFor(li => li.UnitPrice).GreaterThanOrEqualTo(0);
    }
}

public sealed record CreateDocumentCommand(
    DocumentType Type,
    Guid CustomerId,
    Guid TemplateId,
    DateOnly DueDate,
    List<CreateDocumentLineItemRequest> LineItems) : IRequest<DocumentDto>;

public sealed class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.TemplateId).NotEmpty();
        RuleForEach(c => c.LineItems).SetValidator(new CreateDocumentLineItemRequestValidator());
    }
}

public sealed class CreateDocumentCommandHandler(IApplicationDbContext context, IMapper mapper, IDateTime dateTime, ICurrentUserService currentUser)
    : IRequestHandler<CreateDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();

        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var template = await context.Templates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Template), request.TemplateId);

        var issueDate = DateOnly.FromDateTime(dateTime.UtcNow.Date);
        var documentNumber = await DocumentNumbering.GenerateNextAsync(context, workspaceId, request.Type, issueDate, cancellationToken);

        var document = new Document(workspaceId, documentNumber, request.Type, customer.Id, template.Id, issueDate, request.DueDate);

        foreach (var item in request.LineItems)
        {
            document.AddLineItem(item.Description, item.Quantity, item.UnitPrice, item.ProductId);
        }

        template.RecordUsage();
        context.Documents.Add(document);
        await context.SaveChangesAsync(cancellationToken);

        // Reload with navigations populated for a complete response — the entity
        // above only has CustomerId/TemplateId set, not the Customer/Template
        // navigation properties themselves.
        var created = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.Template)
            .Include(d => d.LineItems)
            .FirstAsync(d => d.Id == document.Id, cancellationToken);

        return mapper.Map<DocumentDto>(created);
    }
}
