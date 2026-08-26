using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.WorkspaceId)
            .IsRequired();

        builder.HasIndex(t => t.WorkspaceId);

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnType("text");

        builder.Property(t => t.TargetType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.AccentColor)
            .HasMaxLength(20);

        builder.Property(t => t.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.UsageCount)
            .IsRequired()
            .HasDefaultValue(0);
    }
}
