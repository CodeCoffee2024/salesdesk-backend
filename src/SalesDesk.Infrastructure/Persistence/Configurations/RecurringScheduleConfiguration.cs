using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class RecurringScheduleConfiguration : IEntityTypeConfiguration<RecurringSchedule>
{
    public void Configure(EntityTypeBuilder<RecurringSchedule> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.WorkspaceId).IsRequired();
        builder.HasIndex(s => s.WorkspaceId);

        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Interval).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();
        builder.Property(s => s.ClientCountry).HasMaxLength(2);

        builder.Property(s => s.NextRunDate).IsRequired();
        builder.Property(s => s.DueDateOffsetDays).IsRequired();
        builder.Property(s => s.AutoDispatch).IsRequired();
        builder.Property(s => s.IsActive).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        // The periodic generator scans for exactly this shape every run — active
        // schedules due today or earlier.
        builder.HasIndex(s => new { s.IsActive, s.NextRunDate });

        // Restrict, not cascade: deleting a customer or template must not silently
        // remove a retainer schedule referencing it (same rule as Document).
        builder.HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Template)
            .WithMany()
            .HasForeignKey(s => s.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.LineItems)
            .WithOne(li => li.RecurringSchedule)
            .HasForeignKey(li => li.RecurringScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.LineItems)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
