using Microsoft.Extensions.Logging;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Stand-in <see cref="ICurrencyConversionService"/> for as long as no live FX-rate
/// provider is configured: converts via a small hardcoded table of approximate,
/// point-in-time rates against USD, rather than dispatching to a real-time rate API
/// (e.g. exchangerate.host, Open Exchange Rates). This keeps the dashboard's
/// cross-currency aggregation (TASK-029) fully exercisable without a paid FX
/// subscription being a prerequisite — mirrors how <see cref="LogEmailSender"/>
/// stands in for real email delivery until a provider is chosen. Swap in a real
/// rate-fetching implementation by registering a different
/// <see cref="ICurrencyConversionService"/> in <see cref="DependencyInjection"/>
/// when one is chosen.
///
/// NOTE: these are illustrative approximate rates, not a live feed — do not treat
/// dashboard totals derived from this as financially authoritative.
/// </summary>
public sealed class StaticRateCurrencyConversionService(ILogger<StaticRateCurrencyConversionService> logger) : ICurrencyConversionService
{
    // Approximate units-per-1-USD, as of this table's last manual update. Keyed by
    // ISO 4217 code — not a currency *symbol* table, so it doesn't run afoul of the
    // TASK-029 guardrail against hardcoding symbols/tax rates.
    private static readonly IReadOnlyDictionary<string, decimal> RatesPerUsd = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1.00m,
        ["EUR"] = 0.92m,
        ["GBP"] = 0.79m,
        ["PHP"] = 56.50m,
        ["JPY"] = 149.50m,
        ["CAD"] = 1.36m,
        ["AUD"] = 1.52m,
        ["SGD"] = 1.34m,
        ["INR"] = 83.30m,
        ["CNY"] = 7.24m,
        ["CHF"] = 0.88m,
        ["NZD"] = 1.64m,
        ["MXN"] = 17.10m,
        ["ZAR"] = 18.60m,
        ["AED"] = 3.67m,
    };

    public decimal Convert(decimal amount, string fromCurrency, string toCurrency)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        var fromRate = ResolveRate(fromCurrency);
        var toRate = ResolveRate(toCurrency);

        // amount (fromCurrency) -> USD -> toCurrency
        var amountInUsd = amount / fromRate;
        return amountInUsd * toRate;
    }

    private decimal ResolveRate(string currencyCode)
    {
        if (RatesPerUsd.TryGetValue(currencyCode, out var rate))
        {
            return rate;
        }

        logger.LogWarning(
            "No conversion rate configured for currency {CurrencyCode} — treating it as 1:1 with USD. " +
            "Add it to StaticRateCurrencyConversionService's rate table, or wire up a real FX-rate provider.",
            currencyCode);
        return 1.00m;
    }
}
