using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Tests;

/// <summary>
/// Deterministic <see cref="ICurrencyConversionService"/> test double — same
/// currency is always 1:1, and cross-currency conversions use whatever fixed rate
/// (target units per 1 source unit) the test configures via <see cref="Rates"/>,
/// so dashboard aggregation tests don't depend on StaticRateCurrencyConversionService's
/// real-world table.
/// </summary>
public sealed class FakeCurrencyConversionService : ICurrencyConversionService
{
    /// <summary>Keyed by "FROM-TO" (e.g. "EUR-USD"), both upper-cased.</summary>
    public Dictionary<string, decimal> Rates { get; } = [];

    public decimal Convert(decimal amount, string fromCurrency, string toCurrency)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        var key = $"{fromCurrency.ToUpperInvariant()}-{toCurrency.ToUpperInvariant()}";
        return Rates.TryGetValue(key, out var rate) ? amount * rate : amount;
    }
}
