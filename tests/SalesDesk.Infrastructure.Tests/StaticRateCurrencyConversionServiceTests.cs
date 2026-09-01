using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SalesDesk.Infrastructure.Services;

namespace SalesDesk.Infrastructure.Tests;

public class StaticRateCurrencyConversionServiceTests
{
    private static StaticRateCurrencyConversionService CreateService() => new(NullLogger<StaticRateCurrencyConversionService>.Instance);

    [Fact]
    public void Convert_returns_the_amount_unchanged_when_currencies_match()
    {
        var service = CreateService();

        service.Convert(100m, "USD", "USD").Should().Be(100m);
    }

    [Fact]
    public void Convert_returns_the_amount_unchanged_when_currencies_match_case_insensitively()
    {
        var service = CreateService();

        service.Convert(100m, "usd", "USD").Should().Be(100m);
    }

    [Fact]
    public void Convert_converts_between_two_known_currencies()
    {
        var service = CreateService();

        // Same direction round-trip should return (approximately) the original amount.
        var converted = service.Convert(100m, "USD", "EUR");
        var roundTripped = service.Convert(converted, "EUR", "USD");

        converted.Should().NotBe(100m);
        roundTripped.Should().BeApproximately(100m, 0.01m);
    }

    [Fact]
    public void Convert_treats_an_unrecognized_currency_as_1_to_1_with_USD()
    {
        var service = CreateService();

        // "XYZ" isn't in the table, so it's treated as 1 unit == 1 USD.
        service.Convert(100m, "XYZ", "USD").Should().Be(100m);
    }
}
