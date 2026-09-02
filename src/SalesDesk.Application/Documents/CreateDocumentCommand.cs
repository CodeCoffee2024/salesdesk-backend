using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Billing;
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

/// <param name="Dispatch">TASK-037 "Save &amp; Send to Client" — false leaves the new document as a Draft.</param>
public sealed record CreateDocumentCommand(
    DocumentType Type,
    Guid CustomerId,
    Guid TemplateId,
    DateOnly DueDate,
    List<CreateDocumentLineItemRequest> LineItems,
    string? Currency = null,
    string? ClientCountry = null,
    bool Dispatch = false) : IRequest<DocumentDto>;

public sealed class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.TemplateId).NotEmpty();
        RuleForEach(c => c.LineItems).SetValidator(new CreateDocumentLineItemRequestValidator());

        // Optional overrides (TASK-029) — when omitted, the handler defaults them
        // from the customer/workspace, so only validate shape when actually supplied.
        RuleFor(c => c.Currency).Matches("^[A-Za-z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code.").When(c => c.Currency is not null);
        RuleFor(c => c.ClientCountry).Matches("^[A-Za-z]{2}$").WithMessage("Client country must be a 2-letter ISO 3166-1 alpha-2 code.").When(c => c.ClientCountry is not null);
    }
}

public sealed class CreateDocumentCommandHandler(
    IApplicationDbContext context, IMapper mapper, IDateTime dateTime, ICurrentUserService currentUser, IEmailSender emailSender, IPublicLinkBuilder linkBuilder)
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

        // Not every caller (e.g. some tests) seeds a Workspace row for the current
        // workspace id — fall back to the global defaults rather than throwing, the
        // same way an unset Currency/ClientCountry override falls back below.
        var workspace = await context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);

        var currency = request.Currency ?? workspace?.DefaultCurrency ?? "USD";
        var clientCountry = request.ClientCountry ?? customer.Country ?? workspace?.Country;

        var issueDate = DateOnly.FromDateTime(dateTime.UtcNow.Date);

        // TASK-038: Free tier's "5 active documents/month" cap. No workspace row
        // (e.g. some tests) means no tier to enforce — same permissive fallback as
        // the currency/country defaults above. Checked before reserving a document
        // number so a blocked attempt never burns one.
        if (workspace is not null)
        {
            var monthlyLimit = PricingCatalog.MonthlyDocumentLimit(workspace.SubscriptionTier);
            if (monthlyLimit is not null)
            {
                var monthStart = new DateOnly(issueDate.Year, issueDate.Month, 1);
                var issuedThisMonth = await context.Documents
                    .CountAsync(d => d.WorkspaceId == workspaceId && d.IssueDate >= monthStart, cancellationToken);

                if (issuedThisMonth >= monthlyLimit)
                {
                    throw new PlanLimitExceededException(
                        $"The Free plan allows {monthlyLimit} documents per month. Upgrade to Pro for unlimited documents.");
                }
            }
        }

        var documentNumber = await DocumentNumbering.GenerateNextAsync(context, workspaceId, request.Type, issueDate, cancellationToken);

        var document = new Document(workspaceId, documentNumber, request.Type, customer.Id, template.Id, issueDate, request.DueDate, currency, clientCountry);

        foreach (var item in request.LineItems)
        {
            document.AddLineItem(item.Description, item.Quantity, item.UnitPrice, item.ProductId);
        }

        // A brand-new document is always Draft (see the Document constructor), so
        // Dispatch's EnsureEditable check always passes here.
        if (request.Dispatch)
        {
            document.Dispatch(dateTime.UtcNow.UtcDateTime);
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

        if (request.Dispatch)
        {
            await DocumentDispatchNotifier.NotifyAsync(context, emailSender, linkBuilder, created, workspaceId, cancellationToken);
        }

        return mapper.Map<DocumentDto>(created);
    }
}
