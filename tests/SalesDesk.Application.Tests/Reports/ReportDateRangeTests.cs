using FluentAssertions;
using SalesDesk.Application.Reports;

namespace SalesDesk.Application.Tests.Reports;

public class ReportDateRangeTests
{
    private static readonly DateTimeOffset Today = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly FakeDateTime DateTime = new(Today);

    [Fact]
    public void Resolve_defaults_to_current_month_to_date_when_both_null()
    {
        var (from, to) = ReportDateRange.Resolve(null, null, DateTime);

        from.Should().Be(new DateOnly(2026, 8, 1));
        to.Should().Be(new DateOnly(2026, 8, 25));
    }

    [Fact]
    public void Resolve_defaults_from_to_the_first_of_tos_month_when_only_to_is_given()
    {
        var (from, to) = ReportDateRange.Resolve(null, new DateOnly(2026, 3, 10), DateTime);

        from.Should().Be(new DateOnly(2026, 3, 1));
        to.Should().Be(new DateOnly(2026, 3, 10));
    }

    [Fact]
    public void Resolve_defaults_to_to_today_when_only_from_is_given()
    {
        var (from, to) = ReportDateRange.Resolve(new DateOnly(2026, 1, 1), null, DateTime);

        from.Should().Be(new DateOnly(2026, 1, 1));
        to.Should().Be(new DateOnly(2026, 8, 25));
    }

    [Fact]
    public void Resolve_passes_through_explicit_dates_unchanged()
    {
        var (from, to) = ReportDateRange.Resolve(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), DateTime);

        from.Should().Be(new DateOnly(2026, 1, 1));
        to.Should().Be(new DateOnly(2026, 1, 31));
    }

    [Fact]
    public void Resolve_swaps_the_bounds_when_from_is_after_to()
    {
        var (from, to) = ReportDateRange.Resolve(new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1), DateTime);

        from.Should().Be(new DateOnly(2026, 6, 1));
        to.Should().Be(new DateOnly(2026, 6, 30));
    }
}
