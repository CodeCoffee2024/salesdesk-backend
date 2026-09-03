using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesDesk.Domain.Billing;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Infrastructure.Persistence.Configurations;

public sealed class GCashPaymentSubmissionConfiguration : IEntityTypeConfiguration<GCashPaymentSubmission>
{
    public void Configure(EntityTypeBuilder<GCashPaymentSubmission> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.WorkspaceId);

        builder.Property(s => s.Tier)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.BillingCycle)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.AmountPhp)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.GCashReferenceNumber)
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(s => s.SenderName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.SenderMobileNumber)
            .HasMaxLength(20)
            .IsRequired();

        // Unbounded text, same as DocumentSignature.SignatureImageDataUrl — sized
        // only by the application-level cap on the submitted data URL.
        builder.Property(s => s.ScreenshotDataUrl)
            .HasColumnType("text");

        builder.Property(s => s.ApprovalTokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(s => s.ApprovalTokenHash).IsUnique();

        builder.Property(s => s.IsApproved).IsRequired();

        builder.Property(s => s.ApprovedAtUtc);

        builder.Property(s => s.SubmittedAtUtc).IsRequired();

        builder.Ignore(s => s.SubscriptionLength);
    }
}
