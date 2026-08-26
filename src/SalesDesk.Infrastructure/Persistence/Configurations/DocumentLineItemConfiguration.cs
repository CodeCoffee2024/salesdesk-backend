using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class DocumentLineItemConfiguration : IEntityTypeConfiguration<DocumentLineItem>
{
    public void Configure(EntityTypeBuilder<DocumentLineItem> builder)
    {
        builder.HasKey(li => li.Id);

        builder.Property(li => li.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(li => li.Quantity)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(li => li.UnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(li => li.LineTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        // DocumentId's FK/cascade behavior is configured from the Document side
        // (DocumentConfiguration) to keep the aggregate's ownership rule in one place.

        // Nullable by design: a line item may reference a catalog product for
        // consistent pricing, or stay free-text. Losing the product must not delete
        // billing history — the line item's own description/price snapshot survives.
        builder.HasOne(li => li.Product)
            .WithMany()
            .HasForeignKey(li => li.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
