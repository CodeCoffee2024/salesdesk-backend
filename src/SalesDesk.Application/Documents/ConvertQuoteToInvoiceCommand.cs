using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents;

/// <summary>
/// Backs POST /api/documents/{id}/convert-to-invoice. Creates a brand-new invoice
/// carrying the quote's customer, template and line items — the source quote is
/// left untouched (still Accepted) as the historical record of what was proposed.
/// </summary>
public sealed record ConvertQuoteToInvoiceCommand(Guid QuoteId) : IRequest<DocumentDto>;

public sealed class ConvertQuoteToInvoiceCommandHandler(IApplicationDbContext context, IMapper mapper, IDateTime dateTime, ICurrentUserService currentUser)
    : IRequestHandler<ConvertQuoteToInvoiceCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(ConvertQuoteToInvoiceCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();

        var quote = await context.Documents
            .Include(d => d.LineItems)
            .FirstOrDefaultAsync(d => d.Id == request.QuoteId && d.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.QuoteId);

        if (quote.Type != DocumentType.Quote)
        {
            throw new InvalidOperationException("Only a quote can be converted to an invoice.");
        }

        if (quote.Status != DocumentStatus.Accepted)
        {
            throw new InvalidOperationException("Only an accepted quote can be converted to an invoice.");
        }

        var issueDate = DateOnly.FromDateTime(dateTime.UtcNow.Date);
        // The quote's own due date may already be in the past by the time it's
        // converted — fall back to a fresh 14-day window rather than constructing
        // an invoice whose due date precedes its issue date.
        var dueDate = quote.DueDate >= issueDate ? quote.DueDate : issueDate.AddDays(14);

        var documentNumber = await DocumentNumbering.GenerateNextAsync(context, workspaceId, DocumentType.Invoice, issueDate, cancellationToken);
        // Carries the quote's currency/target-country override forward (TASK-029) —
        // an invoice for an international client shouldn't silently revert to the
        // workspace's default currency just because it started life as a quote.
        var invoice = new Document(
            workspaceId, documentNumber, DocumentType.Invoice, quote.CustomerId, quote.TemplateId, issueDate, dueDate,
            quote.Currency, quote.ClientCountry);

        foreach (var item in quote.LineItems)
        {
            invoice.AddLineItem(item.Description, item.Quantity, item.UnitPrice, item.ProductId);
        }

        context.DocumentActivities.Add(invoice.RecordActivity(DocumentActivityType.Created, $"Converted from quote {quote.DocumentNumber}", dateTime.UtcNow.UtcDateTime));

        context.Documents.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);

        var created = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.Template)
            .Include(d => d.LineItems)
            .Include(d => d.Activities)
            .FirstAsync(d => d.Id == invoice.Id, cancellationToken);

        return mapper.Map<DocumentDto>(created);
    }
}
