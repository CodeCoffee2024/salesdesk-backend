using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Users;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasMaxLength(320)
            .IsRequired();

        // Unlike Customer.Email (a contact detail, not unique), a user's email is
        // their login identifier — one account per address across the platform.
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.WorkspaceId)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.PasswordResetTokenHash)
            .HasMaxLength(64);

        builder.HasIndex(u => u.PasswordResetTokenHash);
    }
}
