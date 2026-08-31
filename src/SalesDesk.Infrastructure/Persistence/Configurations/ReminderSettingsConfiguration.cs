using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class ReminderSettingsConfiguration : IEntityTypeConfiguration<ReminderSettings>
{
    public void Configure(EntityTypeBuilder<ReminderSettings> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CcEmail).HasMaxLength(320);

        builder.HasIndex(s => s.WorkspaceId).IsUnique();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
