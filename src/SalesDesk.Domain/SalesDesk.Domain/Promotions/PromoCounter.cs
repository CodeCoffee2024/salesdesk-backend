using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Promotions;

/// <summary>
/// TASK-031: a single seeded row tracking how many accounts have so far claimed
/// the "Early 100 Free Year" promo. Deliberately a passive record rather than an
/// aggregate with its own increment method — the actual reservation is an atomic
/// `UPDATE promo_counters SET count = count + 1 WHERE key = ... AND count &lt; 100`
/// executed directly by IApplicationDbContext.TryReserveEarlyBirdPromoSlotAsync
/// (see its own doc comment for the concurrency reasoning), not a
/// read-then-SaveChanges cycle through this entity's change-tracked properties.
/// This entity exists so that statement has a row to target and so <see cref="Count"/>
/// stays queryable/inspectable like any other persisted value.
/// </summary>
public sealed class PromoCounter : Entity
{
    /// <summary>Fixed id for the one "Early 100 Free Year" counter row — seeded via the AddSubscriptionPromo migration's HasData, never created at runtime.</summary>
    public static readonly Guid EarlyBirdPromoId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public const string EarlyBirdPremiumKey = "early_bird_premium";

    public const int EarlyBirdCap = 100;

    public string Key { get; private set; }

    public int Count { get; private set; }

    private PromoCounter()
    {
        Key = string.Empty;
    }

    public PromoCounter(string key)
    {
        Key = Guard.AgainstNullOrWhiteSpace(key, nameof(key));
        Count = 0;
    }
}
