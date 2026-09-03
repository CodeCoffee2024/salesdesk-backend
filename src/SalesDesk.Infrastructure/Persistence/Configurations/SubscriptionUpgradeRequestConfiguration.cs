using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Billing;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionUpgradeRequestConfiguration : IEntityTypeConfiguration<SubscriptionUpgradeRequest>
{
    public void Configure(EntityTypeBuilder<SubscriptionUpgradeRequest> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.WorkspaceId);

        builder.Property(r => r.Tier)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.BillingCycle)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Note)
            .HasMaxLength(2000);

        builder.Property(r => r.ApprovalTokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(r => r.ApprovalTokenHash).IsUnique();

        builder.Property(r => r.IsApproved).IsRequired();

        builder.Property(r => r.ApprovedAtUtc);

        builder.Property(r => r.RequestedAtUtc).IsRequired();

        builder.Ignore(r => r.SubscriptionLength);
    }
}
