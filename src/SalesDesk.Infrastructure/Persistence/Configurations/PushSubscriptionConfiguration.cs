using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Users;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Endpoint).HasColumnType("text").IsRequired();
        builder.Property(s => s.P256dhKey).HasMaxLength(200).IsRequired();
        builder.Property(s => s.AuthKey).HasMaxLength(100).IsRequired();

        // Re-subscribing the same browser is an upsert keyed on the endpoint URL,
        // not a new row per subscribe call.
        builder.HasIndex(s => s.Endpoint).IsUnique();
        builder.HasIndex(s => s.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
