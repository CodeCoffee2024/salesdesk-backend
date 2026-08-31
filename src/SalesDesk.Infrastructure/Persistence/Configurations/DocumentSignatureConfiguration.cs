using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class DocumentSignatureConfiguration : IEntityTypeConfiguration<DocumentSignature>
{
    public void Configure(EntityTypeBuilder<DocumentSignature> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SignerName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.SignerEmail).HasMaxLength(320).IsRequired();
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.SignatureImageDataUrl).HasColumnType("text").IsRequired();
        builder.Property(s => s.IpAddress).HasMaxLength(64).IsRequired();
        builder.Property(s => s.UserAgent).HasMaxLength(512).IsRequired();
        builder.Property(s => s.DocumentHash).HasMaxLength(64).IsRequired();
        builder.Property(s => s.SignedAtUtc).IsRequired();

        // One signature per document — its presence is what locks the document
        // (Document.IsLocked), so a second row for the same document must be impossible.
        builder.HasIndex(s => s.DocumentId).IsUnique();

        builder.HasOne(s => s.Document)
            .WithOne(d => d.Signature)
            .HasForeignKey<DocumentSignature>(s => s.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
