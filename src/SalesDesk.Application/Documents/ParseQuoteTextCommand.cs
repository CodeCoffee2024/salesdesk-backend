using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Application.Documents;

public sealed class ParsedLineItemDto
{
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

/// <summary>
/// Result of TASK-033's AI text-parsing pass: what the model extracted, whether a
/// new Customer had to be provisioned to hold it, and which fields it couldn't
/// find in the pasted text (so the frontend's pre-filled form can flag them for
/// the user instead of silently leaving them blank). This is an MVP pass: it
/// resolves and creates the Customer, but deposit/validity are surfaced as
/// suggestions only — there's no persisted field for them yet on Document.
/// </summary>
public sealed class ParsedQuoteResultDto
{
    /// <summary>Empty Guid when no customer could be resolved or created (e.g. the text had no email at all) — the frontend leaves the customer picker for the user in that case.</summary>
    public Guid CustomerId { get; init; }
    public bool CustomerCreated { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public List<ParsedLineItemDto> LineItems { get; init; } = [];
    public decimal? SuggestedDepositPercentage { get; init; }
    public int? SuggestedValidityDays { get; init; }
    /// <summary>ISO 4217 code the parser is confident the text specified (e.g. an explicit "Php"/"₱"/city context). Null when the text gave no real currency signal, in which case the frontend leaves the form's existing currency untouched rather than assuming USD.</summary>
    public string? SuggestedCurrency { get; init; }
    public List<string> UnresolvedFields { get; init; } = [];
}

public sealed record ParseQuoteTextCommand(string RawText) : IRequest<ParsedQuoteResultDto>;

public sealed class ParseQuoteTextCommandValidator : AbstractValidator<ParseQuoteTextCommand>
{
    public ParseQuoteTextCommandValidator()
    {
        RuleFor(c => c.RawText).NotEmpty();
        RuleFor(c => c.RawText).MaximumLength(5000).WithMessage("Pasted text is limited to 5000 characters.");
    }
}

public sealed class ParseQuoteTextCommandHandler(IApplicationDbContext context, IQuoteTextParser parser, ICurrentUserService currentUser)
    : IRequestHandler<ParseQuoteTextCommand, ParsedQuoteResultDto>
{
    public async Task<ParsedQuoteResultDto> Handle(ParseQuoteTextCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var parsed = await parser.ParseAsync(request.RawText, cancellationToken);

        var unresolvedFields = new List<string>();

        var email = parsed.Customer.Email?.Trim();
        Customer? customer = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            // No Duplicate Customers guardrail: exact email match against the
            // workspace's existing customers before ever creating a new one.
            customer = await context.Customers
                .FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.Email.ToLower() == email.ToLower(), cancellationToken);
        }
        else
        {
            unresolvedFields.Add("customer email");
        }

        var customerCreated = false;
        if (customer is null)
        {
            var name = parsed.Customer.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                unresolvedFields.Add("customer name");
            }

            // Customer.Email is required (Guard.AgainstNullOrWhiteSpace) — without
            // one there's nothing to auto-provision, so this customer is left
            // unresolved for the user to pick or create manually on the form.
            if (!string.IsNullOrWhiteSpace(email))
            {
                var resolvedName = string.IsNullOrWhiteSpace(name) ? "New customer" : name!;
                // Most of this app's ICP (freelance event hosts) bills individuals,
                // not companies, so a client with no company mentioned uses their
                // own name rather than leaving the required Company field empty.
                var company = string.IsNullOrWhiteSpace(parsed.Customer.Company) ? resolvedName : parsed.Customer.Company!.Trim();

                customer = new Customer(workspaceId, resolvedName, company, email, parsed.Customer.Phone?.Trim());
                context.Customers.Add(customer);
                customerCreated = true;
            }
        }

        if (parsed.LineItems.Count == 0)
        {
            unresolvedFields.Add("line items");
        }

        if (customerCreated)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return new ParsedQuoteResultDto
        {
            CustomerId = customer?.Id ?? Guid.Empty,
            CustomerCreated = customerCreated,
            CustomerName = customer?.Name ?? parsed.Customer.Name ?? string.Empty,
            LineItems = parsed.LineItems
                .Select(li => new ParsedLineItemDto { Description = li.Description, Quantity = li.Quantity, UnitPrice = li.UnitPrice })
                .ToList(),
            SuggestedDepositPercentage = parsed.DepositPercentage,
            SuggestedValidityDays = parsed.ValidityDays,
            SuggestedCurrency = parsed.Currency,
            UnresolvedFields = unresolvedFields
        };
    }
}
