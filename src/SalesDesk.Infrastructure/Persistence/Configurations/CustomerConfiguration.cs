using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Customers;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.WorkspaceId)
            .IsRequired();

        builder.HasIndex(c => c.WorkspaceId);

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Company)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(c => c.Phone)
            .HasMaxLength(50);

        builder.Property(c => c.Country)
            .HasMaxLength(2);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        // Supports "search by customer" / lookup-by-email flows without being a
        // uniqueness constraint — the reference product does not treat email as unique.
        builder.HasIndex(c => c.Email);
    }
}
