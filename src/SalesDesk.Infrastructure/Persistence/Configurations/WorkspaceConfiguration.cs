using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(w => w.Tagline)
            .HasMaxLength(300);

        builder.Property(w => w.Address)
            .HasMaxLength(500);

        builder.Property(w => w.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(w => w.LogoUrl)
            .HasMaxLength(2048);

        builder.Property(w => w.Country)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(w => w.DefaultCurrency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(w => w.IsActive)
            .IsRequired();

        builder.Property(w => w.DocumentQuota);

        builder.Property(w => w.SubscriptionTier)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(SubscriptionTier.Free)
            .IsRequired();

        builder.Property(w => w.SubscriptionEndDate);

        builder.Property(w => w.IsEarlyBirdPromo)
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .IsRequired();
    }
}
