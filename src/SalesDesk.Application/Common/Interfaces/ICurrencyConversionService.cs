namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// Converts a monetary amount from one ISO 4217 currency into another so the
/// dashboard can normalize documents issued in different currencies into the
/// workspace's own <c>DefaultCurrency</c> for aggregate metrics (Revenue,
/// Outstanding, Quote Pipeline) — see <c>GetDashboardSummaryQueryHandler</c>
/// (TASK-029).
///
/// Implemented in Infrastructure; until a real FX-rate provider is wired up, the
/// registered implementation uses a small static conversion table rather than a
/// live rate feed — the same "stand-in now, real provider later" shape as
/// <see cref="IEmailSender"/>/<see cref="IPushNotificationSender"/>. This is
/// deliberately a numeric *rate* table, not a currency *symbol* table — the
/// TASK-029 guardrail forbids hardcoding symbols/tax rates, not exchange rates,
/// which have no ISO-standardized source to defer to.
/// </summary>
public interface ICurrencyConversionService
{
    /// <summary>Converts <paramref name="amount"/> from <paramref name="fromCurrency"/> into <paramref name="toCurrency"/> (both ISO 4217 codes). Returns <paramref name="amount"/> unchanged when the codes match or either is unrecognized.</summary>
    decimal Convert(decimal amount, string fromCurrency, string toCurrency);
}
