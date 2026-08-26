using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Audit;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Message)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(a => a.OccurredAtUtc)
            .IsRequired();

        // Newest-first is the console's default read pattern (TASK-017 AC4).
        builder.HasIndex(a => a.OccurredAtUtc);
    }
}
