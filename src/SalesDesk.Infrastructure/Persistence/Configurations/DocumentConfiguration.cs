using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.WorkspaceId)
            .IsRequired();

        builder.HasIndex(d => d.WorkspaceId);

        builder.Property(d => d.PublicToken)
            .IsRequired();

        // Looked up directly by an anonymous client hitting /view/{token} (TASK-023/024)
        // — must be unique and fast, with no workspace scoping possible at that point.
        builder.HasIndex(d => d.PublicToken)
            .IsUnique();

        builder.Property(d => d.DocumentNumber)
            .HasMaxLength(30)
            .IsRequired();

        // Document numbers (e.g. "QUO-2026-035") are the human-facing identifier and
        // must never collide within a workspace — numbering restarts per workspace
        // (see DocumentNumbering), so uniqueness is scoped by WorkspaceId rather than
        // global.
        builder.HasIndex(d => new { d.WorkspaceId, d.DocumentNumber })
            .IsUnique();

        builder.Property(d => d.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.IssueDate)
            .IsRequired();

        builder.Property(d => d.DueDate)
            .IsRequired();

        builder.Property(d => d.Subtotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(d => d.Total)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(d => d.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(d => d.ClientCountry)
            .HasMaxLength(2);

        // Restrict, not cascade: deleting a customer or template must not silently
        // wipe out financial records that reference it.
        builder.HasOne(d => d.Customer)
            .WithMany()
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Template)
            .WithMany()
            .HasForeignKey(d => d.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Line items are owned by the document: deleting a document deletes its items.
        builder.HasMany(d => d.LineItems)
            .WithOne(li => li.Document)
            .HasForeignKey(li => li.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.LineItems)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(d => d.CustomerId);
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.Type);
    }
}
