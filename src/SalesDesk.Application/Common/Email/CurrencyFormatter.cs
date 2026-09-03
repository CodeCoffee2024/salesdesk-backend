using System.Collections.Concurrent;
using System.Globalization;

namespace SalesDesk.Application.Common.Email;

/// <summary>
/// Formats a document's Total/Amount for the plain-text/HTML email templates,
/// honoring the document's own ISO 4217 <c>Currency</c> field — the same value
/// the frontend already formats correctly everywhere else via
/// <c>Intl.NumberFormat(locale, { style: 'currency', currency })</c>
/// (locale.util.ts). Every email template here used `{amount:C}` instead, which
/// formats with the server process's own current culture regardless of the
/// document's actual currency — a PHP-priced invoice would render "$450.00" in
/// its emails no matter what was actually agreed, and any currency whose symbol
/// the ambient culture doesn't know shows the wrong one silently rather than
/// failing loudly.
/// </summary>
internal static class CurrencyFormatter
{
    // Every specific (region-bearing) culture the runtime knows about, keyed by
    // the ISO 4217 code its region actually uses — computed once, since scanning
    // ~700 cultures per email would be wasteful. Where more than one culture
    // shares a currency (most of the Eurozone, for instance), the first match is
    // used; only the symbol and decimal convention matter here; regional
    // grouping/decimal-separator flourishes are a cosmetic tie-break, not a
    // correctness requirement.
    private static readonly Lazy<IReadOnlyDictionary<string, CultureInfo>> CultureByCurrency = new(BuildCultureByCurrencyMap);

    private static readonly ConcurrentDictionary<string, CultureInfo?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string Format(decimal amount, string currencyCode)
    {
        var culture = Cache.GetOrAdd(currencyCode, code =>
            CultureByCurrency.Value.TryGetValue(code.ToUpperInvariant(), out var found) ? found : null);

        // No culture on this runtime uses the code as-is (a typo, or a currency
        // ICU doesn't carry region data for) — an ISO-prefixed plain number is
        // always unambiguous and never silently wrong, unlike guessing a symbol.
        return culture is null
            ? $"{currencyCode.ToUpperInvariant()} {amount.ToString("N2", CultureInfo.InvariantCulture)}"
            : amount.ToString("C", culture);
    }

    private static Dictionary<string, CultureInfo> BuildCultureByCurrencyMap()
    {
        var map = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            string isoCurrencySymbol;
            try
            {
                isoCurrencySymbol = new RegionInfo(culture.Name).ISOCurrencySymbol;
            }
            catch (ArgumentException)
            {
                // A handful of specific cultures (e.g. some custom/legacy ones)
                // carry no region data RegionInfo can resolve — skip rather than fail.
                continue;
            }

            map.TryAdd(isoCurrencySymbol, culture);
        }

        return map;
    }
}
