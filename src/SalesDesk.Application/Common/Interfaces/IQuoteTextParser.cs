namespace SalesDesk.Application.Common.Interfaces;

/// <summary>Customer fields the parser found in the pasted text — any of these may be null when the text didn't mention them.</summary>
public sealed record ParsedCustomerText(string? Name, string? Email, string? Phone, string? Company);

/// <summary>
/// One extracted line item. Only the raw inputs an amount is built from, never a
/// computed line total, subtotal, or grand total (TASK-033's "Deterministic Math
/// Guardrail": the LLM extracts numbers, it never does arithmetic on them).
/// </summary>
public sealed record ParsedLineItemText(string Description, decimal Quantity, decimal UnitPrice);

public sealed record ParsedQuoteText(
    ParsedCustomerText Customer,
    List<ParsedLineItemText> LineItems,
    decimal? DepositPercentage,
    int? ValidityDays);

/// <summary>
/// Extracts structured quote/invoice data from unstructured pasted text (TASK-033).
/// Implemented by SalesDesk.Infrastructure against whichever LLM provider is
/// configured; see DependencyInjection for the fallback when none is.
/// </summary>
public interface IQuoteTextParser
{
    Task<ParsedQuoteText> ParseAsync(string rawText, CancellationToken cancellationToken);
}
