using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class DocumentActivityConfiguration : IEntityTypeConfiguration<DocumentActivity>
{
    public void Configure(EntityTypeBuilder<DocumentActivity> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.Property(a => a.Detail).HasMaxLength(2000);

        builder.Property(a => a.OccurredAtUtc).IsRequired();

        // Reading a document's full timeline is the only access pattern — always
        // "every row for this DocumentId, ordered by time."
        builder.HasIndex(a => new { a.DocumentId, a.OccurredAtUtc });

        // The relationship itself (HasMany/WithOne, cascade delete, the
        // UsePropertyAccessMode fix for Document.Activities' read-only wrapper
        // property) is configured from the Document side — see DocumentConfiguration,
        // matching how Document.LineItems is set up.
    }
}
