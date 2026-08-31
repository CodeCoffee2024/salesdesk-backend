using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class DocumentReminderLogConfiguration : IEntityTypeConfiguration<DocumentReminderLog>
{
    public void Configure(EntityTypeBuilder<DocumentReminderLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(l => l.SentAtUtc).IsRequired();

        // Each rule fires at most once per document — this is the dispatch engine's
        // idempotency guarantee (TASK-025), enforced at the database level rather
        // than only in application code.
        builder.HasIndex(l => new { l.DocumentId, l.Type }).IsUnique();

        builder.HasOne(l => l.Document)
            .WithMany()
            .HasForeignKey(l => l.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
