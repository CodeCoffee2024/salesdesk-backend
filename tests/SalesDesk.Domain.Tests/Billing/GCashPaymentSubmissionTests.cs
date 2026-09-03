using FluentAssertions;
using SalesDesk.Domain.Billing;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Domain.Tests.Billing;

public class GCashPaymentSubmissionTests
{
    private static GCashPaymentSubmission Make(
        SubscriptionTier tier = SubscriptionTier.Pro,
        string billingCycle = "Monthly",
        string gcashReferenceNumber = "1234567890123") =>
        new(
            Guid.NewGuid(), tier, billingCycle, 199m, gcashReferenceNumber,
            "Maya Chen", "09171234567", null, "hash", DateTimeOffset.UtcNow);

    [Fact]
    public void Constructor_rejects_the_Free_tier()
    {
        var act = () => Make(tier: SubscriptionTier.Free);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("Weekly")]
    [InlineData("")]
    public void Constructor_rejects_an_invalid_billing_cycle(string billingCycle)
    {
        var act = () => Make(billingCycle: billingCycle);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("123456789012")] // 12 digits
    [InlineData("12345678901234")] // 14 digits
    [InlineData("123456789012A")] // non-digit
    public void Constructor_rejects_a_reference_number_that_is_not_exactly_13_digits(string reference)
    {
        var act = () => Make(gcashReferenceNumber: reference);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Approve_sets_IsApproved_and_ApprovedAtUtc()
    {
        var submission = Make();
        var approvedAt = new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

        submission.Approve(approvedAt);

        submission.IsApproved.Should().BeTrue();
        submission.ApprovedAtUtc.Should().Be(approvedAt);
    }

    [Fact]
    public void Approve_is_idempotent_and_keeps_the_first_approval_timestamp()
    {
        var submission = Make();
        var firstApproval = new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);
        var secondApproval = firstApproval.AddHours(3);

        submission.Approve(firstApproval);
        submission.Approve(secondApproval);

        submission.ApprovedAtUtc.Should().Be(firstApproval);
    }

    [Fact]
    public void SubscriptionLength_is_30_days_for_Monthly_and_365_for_Annual()
    {
        Make(billingCycle: "Monthly").SubscriptionLength.Should().Be(TimeSpan.FromDays(30));
        Make(billingCycle: "Annual").SubscriptionLength.Should().Be(TimeSpan.FromDays(365));
    }
}
