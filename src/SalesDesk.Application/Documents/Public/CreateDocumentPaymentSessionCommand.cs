using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents.Public;

/// <summary>
/// Backs POST /api/public/documents/{token}/payment-session (TASK-042) — the
/// client's "Pay Now" action on an invoice. Creates a Stripe Checkout session
/// and returns its hosted URL for the frontend to redirect to; the invoice
/// itself isn't marked Paid here — that only happens once Stripe's webhook
/// confirms the session actually completed (see RecordDocumentPaymentCommand).
/// </summary>
public sealed record CreateDocumentPaymentSessionCommand(Guid Token) : IRequest<CheckoutSessionDto>;

public sealed record CheckoutSessionDto(string CheckoutUrl);

public sealed class CreateDocumentPaymentSessionCommandHandler(
    IApplicationDbContext context, IInvoicePaymentGatewayService gateway, IPublicLinkBuilder linkBuilder)
    : IRequestHandler<CreateDocumentPaymentSessionCommand, CheckoutSessionDto>
{
    public async Task<CheckoutSessionDto> Handle(CreateDocumentPaymentSessionCommand request, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(d => d.Customer)
            .FirstOrDefaultAsync(d => d.PublicToken == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Token);

        // Fail before ever calling out to Stripe — a Quote has nothing to pay, and
        // a Paid invoice doesn't need another checkout attempt. SetCheckoutSession
        // repeats this same guard once the session comes back, as a defense-in-depth
        // check on the domain invariant rather than the only place it's enforced.
        if (document.Type != DocumentType.Invoice)
        {
            throw new InvalidOperationException($"Document '{document.Id}' is a {document.Type}, not an Invoice, and can't be paid.");
        }

        if (document.PaymentStatus == PaymentStatus.Paid)
        {
            throw new InvalidOperationException($"Document '{document.Id}' has already been paid.");
        }

        var documentUrl = linkBuilder.BuildDocumentUrl(document.PublicToken);

        var session = await gateway.CreateCheckoutSessionAsync(
            document.Id,
            document.DocumentNumber,
            document.Total,
            document.Currency,
            document.Customer?.Email ?? string.Empty,
            successUrl: $"{documentUrl}?payment=success",
            cancelUrl: $"{documentUrl}?payment=cancelled",
            cancellationToken);

        document.SetCheckoutSession(session.ProviderReference);
        await context.SaveChangesAsync(cancellationToken);

        return new CheckoutSessionDto(session.CheckoutUrl);
    }
}
