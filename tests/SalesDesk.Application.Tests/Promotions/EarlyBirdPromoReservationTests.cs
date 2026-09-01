using FluentAssertions;
using SalesDesk.Domain.Promotions;

namespace SalesDesk.Application.Tests.Promotions;

/// <summary>
/// Exercises IApplicationDbContext.TryReserveEarlyBirdPromoSlotAsync directly
/// (rather than only indirectly through RegisterCommandHandler) so the boundary
/// behavior at exactly PromoCounter.EarlyBirdCap is pinned down on its own —
/// see the method's own doc comment on IApplicationDbContext for why a single
/// `UPDATE ... WHERE count &lt; cap` statement is what makes this safe under
/// concurrent callers, not just correct for a single caller.
/// </summary>
public class EarlyBirdPromoReservationTests
{
    [Fact]
    public async Task TryReserveEarlyBirdPromoSlotAsync_succeeds_while_under_the_cap()
    {
        using var fixture = new SqliteApplicationDbContextFixture();

        var reserved = await fixture.Context.TryReserveEarlyBirdPromoSlotAsync(CancellationToken.None);

        reserved.Should().BeTrue();
    }

    [Fact]
    public async Task TryReserveEarlyBirdPromoSlotAsync_allows_exactly_EarlyBirdCap_reservations_then_stops()
    {
        using var fixture = new SqliteApplicationDbContextFixture();

        for (var i = 0; i < PromoCounter.EarlyBirdCap; i++)
        {
            var reserved = await fixture.Context.TryReserveEarlyBirdPromoSlotAsync(CancellationToken.None);
            reserved.Should().BeTrue($"reservation #{i + 1} of {PromoCounter.EarlyBirdCap} should still be under the cap");
        }

        var oneOverTheCap = await fixture.Context.TryReserveEarlyBirdPromoSlotAsync(CancellationToken.None);

        oneOverTheCap.Should().BeFalse();

        var counter = fixture.CreateContext().PromoCounters.Single(p => p.Key == PromoCounter.EarlyBirdPremiumKey);
        counter.Count.Should().Be(PromoCounter.EarlyBirdCap);
    }
}
