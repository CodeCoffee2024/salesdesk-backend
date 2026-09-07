using FluentAssertions;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Domain.Tests;

public class RecurringScheduleTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TemplateId = Guid.NewGuid();

    private static RecurringSchedule CreateSchedule(RecurrenceInterval interval = RecurrenceInterval.Monthly) =>
        new(WorkspaceId, CustomerId, TemplateId, DocumentType.Invoice, new DateOnly(2026, 9, 1), interval, dueDateOffsetDays: 14, autoDispatch: false);

    [Fact]
    public void Constructor_defaults_to_active_with_NextRunDate_set_to_the_start_date()
    {
        var schedule = CreateSchedule();

        schedule.IsActive.Should().BeTrue();
        schedule.NextRunDate.Should().Be(new DateOnly(2026, 9, 1));
        schedule.LineItems.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_rejects_a_negative_due_date_offset()
    {
        var act = () => new RecurringSchedule(
            WorkspaceId, CustomerId, TemplateId, DocumentType.Invoice, new DateOnly(2026, 9, 1),
            RecurrenceInterval.Monthly, dueDateOffsetDays: -1, autoDispatch: false);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddLineItem_appends_to_LineItems()
    {
        var schedule = CreateSchedule();

        schedule.AddLineItem("Monthly retainer", 1m, 500m);

        schedule.LineItems.Should().ContainSingle(li => li.Description == "Monthly retainer" && li.UnitPrice == 500m);
    }

    [Fact]
    public void Pause_then_Resume_toggles_IsActive_without_touching_NextRunDate()
    {
        var schedule = CreateSchedule();

        schedule.Pause();
        schedule.IsActive.Should().BeFalse();
        schedule.NextRunDate.Should().Be(new DateOnly(2026, 9, 1));

        schedule.Resume();
        schedule.IsActive.Should().BeTrue();
        schedule.NextRunDate.Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void RecordRun_advances_NextRunDate_to_whatever_the_caller_passes()
    {
        var schedule = CreateSchedule();

        schedule.RecordRun(new DateOnly(2026, 10, 1));

        schedule.NextRunDate.Should().Be(new DateOnly(2026, 10, 1));
    }

    [Theory]
    [InlineData(RecurrenceInterval.Weekly, "2026-09-08")]
    [InlineData(RecurrenceInterval.Monthly, "2026-10-01")]
    [InlineData(RecurrenceInterval.Quarterly, "2026-12-01")]
    [InlineData(RecurrenceInterval.Yearly, "2027-09-01")]
    public void AddTo_advances_by_exactly_one_period(RecurrenceInterval interval, string expected)
    {
        var next = interval.AddTo(new DateOnly(2026, 9, 1));

        next.Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public void AddTo_Monthly_clamps_to_the_shorter_target_month_instead_of_overflowing()
    {
        var next = RecurrenceInterval.Monthly.AddTo(new DateOnly(2026, 1, 31));

        next.Should().Be(new DateOnly(2026, 2, 28));
    }
}
