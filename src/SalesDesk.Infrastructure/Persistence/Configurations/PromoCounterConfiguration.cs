using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Promotions;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class PromoCounterConfiguration : IEntityTypeConfiguration<PromoCounter>
{
    public void Configure(EntityTypeBuilder<PromoCounter> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(p => p.Key).IsUnique();

        builder.Property(p => p.Count)
            .IsRequired();

        // TASK-031: the one "Early 100 Free Year" counter row must exist before
        // the first registration ever runs — seeded here via migration data
        // rather than a lazy find-or-create in the handler, so there's no
        // first-ever-registration race to also guard against (a plain unique-key
        // insert race, distinct from the promo-slot race TryReserveEarlyBirdPromoSlotAsync
        // itself guards against).
        builder.HasData(new
        {
            Id = PromoCounter.EarlyBirdPromoId,
            Key = PromoCounter.EarlyBirdPremiumKey,
            Count = 0
        });
    }
}
