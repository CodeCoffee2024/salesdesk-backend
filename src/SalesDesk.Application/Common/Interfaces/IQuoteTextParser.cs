namespace SalesDesk.Application.Common.Interfaces;

/// <summary>Customer fields the parser found in the pasted text — any of these may be null when the text didn't mention them.</summary>
public sealed record ParsedCustomerText(string? Name, string? Email, string? Phone, string? Company);

/// <summary>
/// One extracted line item. Only the raw inputs an amount is built from, never a
/// computed line total, subtotal, or grand total (TASK-033's "Deterministic Math
/// Guardrail": the LLM extracts numbers, it never does arithmetic on them).
/// </summary>
public sealed record ParsedLineItemText(string Description, decimal Quantity, decimal UnitPrice);

/// <summary>Currency is an ISO 4217 code (e.g. "PHP") only when the text gives a real signal (an explicit currency symbol/code, or unambiguous regional context); null, never a guess, when the text is currency-agnostic. A bare "$" or an unmarked number stays null rather than being asserted as USD, since USD is just the form's own pre-existing default, not something the parser confirmed.</summary>
public sealed record ParsedQuoteText(
    ParsedCustomerText Customer,
    List<ParsedLineItemText> LineItems,
    decimal? DepositPercentage,
    int? ValidityDays,
    string? Currency);

/// <summary>
/// Extracts structured quote/invoice data from unstructured pasted text (TASK-033).
/// Implemented by SalesDesk.Infrastructure against whichever LLM provider is
/// configured; see DependencyInjection for the fallback when none is.
/// </summary>
public interface IQuoteTextParser
{
    Task<ParsedQuoteText> ParseAsync(string rawText, CancellationToken cancellationToken);
}
