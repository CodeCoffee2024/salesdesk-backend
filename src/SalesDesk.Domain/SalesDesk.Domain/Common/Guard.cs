namespace SalesDesk.Domain.Common;

/// <summary>
/// Small set of precondition checks shared by domain entity constructors and
/// mutators, so invariants read the same way everywhere they're enforced.
/// </summary>
internal static class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
        }

        return value;
    }

    public static Guid AgainstEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be an empty GUID.", paramName);
        }

        return value;
    }

    public static decimal AgainstNegative(decimal value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
        }

        return value;
    }

    public static decimal AgainstNegativeOrZero(decimal value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
        }

        return value;
    }

    /// <summary>
    /// Normalizes and validates a required ISO code (e.g. an ISO 3166-1 alpha-2
    /// country code or an ISO 4217 currency code) — trims, upper-cases, and rejects
    /// anything that isn't exactly <paramref name="length"/> letters. Used instead of
    /// a hardcoded lookup table so callers stay free to accept any valid code rather
    /// than a curated subset (see TASK-029 guardrail against hardcoding countries).
    /// </summary>
    public static string AgainstInvalidIsoCode(string? value, int length, string paramName)
    {
        var normalized = AgainstNullOrWhiteSpace(value, paramName).Trim().ToUpperInvariant();
        if (normalized.Length != length || !normalized.All(c => c is >= 'A' and <= 'Z'))
        {
            throw new ArgumentException($"'{paramName}' must be a {length}-letter ISO code.", paramName);
        }

        return normalized;
    }

    /// <summary>Same as <see cref="AgainstInvalidIsoCode"/> but for an optional code — null/whitespace passes through as null instead of throwing.</summary>
    public static string? AgainstInvalidIsoCodeOrNull(string? value, int length, string paramName) =>
        string.IsNullOrWhiteSpace(value) ? null : AgainstInvalidIsoCode(value, length, paramName);
}
