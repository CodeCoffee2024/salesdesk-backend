using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class RecurringScheduleLineItemConfiguration : IEntityTypeConfiguration<RecurringScheduleLineItem>
{
    public void Configure(EntityTypeBuilder<RecurringScheduleLineItem> builder)
    {
        builder.HasKey(li => li.Id);

        builder.Property(li => li.Description).IsRequired();
        builder.Property(li => li.Quantity).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(li => li.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();

        // Same rule as DocumentLineItem: a deleted product must not take a
        // still-active recurring schedule's line item with it.
        builder.HasOne(li => li.Product)
            .WithMany()
            .HasForeignKey(li => li.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
