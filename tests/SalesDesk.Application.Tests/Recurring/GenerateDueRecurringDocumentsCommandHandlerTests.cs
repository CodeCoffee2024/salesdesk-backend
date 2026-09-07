using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Recurring;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Recurring;

public class GenerateDueRecurringDocumentsCommandHandlerTests
{
    private static readonly DateTimeOffset Today = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static async Task<(RecurringSchedule Schedule, Customer Customer, Template Template)> SeedDueScheduleAsync(
        SqliteApplicationDbContextFixture fixture, DateOnly nextRunDate, bool autoDispatch = false)
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);

        var schedule = new RecurringSchedule(
            workspace.Id, customer.Id, template.Id, DocumentType.Invoice, nextRunDate,
            RecurrenceInterval.Monthly, dueDateOffsetDays: 14, autoDispatch: autoDispatch);
        schedule.AddLineItem("Monthly retainer", 1m, 500m);

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.RecurringSchedules.Add(schedule);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        return (schedule, customer, template);
    }

    [Fact]
    public async Task Handle_generates_a_draft_document_for_a_due_schedule_and_advances_NextRunDate()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (schedule, customer, _) = await SeedDueScheduleAsync(fixture, new DateOnly(2026, 9, 1));

        var handler = new GenerateDueRecurringDocumentsCommandHandler(fixture.Context, new FakeDateTime(Today), new FakeEmailSender(), new FakePublicLinkBuilder());

        var generatedCount = await handler.Handle(new GenerateDueRecurringDocumentsCommand(), CancellationToken.None);

        generatedCount.Should().Be(1);

        var document = await fixture.Context.Documents.Include(d => d.LineItems).SingleAsync(d => d.CustomerId == customer.Id);
        document.Status.Should().Be(DocumentStatus.Draft);
        document.IssueDate.Should().Be(new DateOnly(2026, 9, 1));
        document.DueDate.Should().Be(new DateOnly(2026, 9, 15));
        document.LineItems.Should().ContainSingle(li => li.Description == "Monthly retainer");

        var updatedSchedule = await fixture.Context.RecurringSchedules.SingleAsync(s => s.Id == schedule.Id);
        updatedSchedule.NextRunDate.Should().Be(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public async Task Handle_dispatches_immediately_when_the_schedule_has_AutoDispatch_enabled()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        await SeedDueScheduleAsync(fixture, new DateOnly(2026, 9, 1), autoDispatch: true);

        var handler = new GenerateDueRecurringDocumentsCommandHandler(fixture.Context, new FakeDateTime(Today), new FakeEmailSender(), new FakePublicLinkBuilder());

        await handler.Handle(new GenerateDueRecurringDocumentsCommand(), CancellationToken.None);

        var document = await fixture.Context.Documents.SingleAsync();
        document.Status.Should().Be(DocumentStatus.Sent);
        document.IsDispatched.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_skips_a_schedule_that_is_not_yet_due()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (schedule, _, _) = await SeedDueScheduleAsync(fixture, new DateOnly(2026, 10, 1));

        var handler = new GenerateDueRecurringDocumentsCommandHandler(fixture.Context, new FakeDateTime(Today), new FakeEmailSender(), new FakePublicLinkBuilder());

        var generatedCount = await handler.Handle(new GenerateDueRecurringDocumentsCommand(), CancellationToken.None);

        generatedCount.Should().Be(0);
        (await fixture.Context.Documents.CountAsync()).Should().Be(0);

        var untouchedSchedule = await fixture.Context.RecurringSchedules.SingleAsync(s => s.Id == schedule.Id);
        untouchedSchedule.NextRunDate.Should().Be(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public async Task Handle_skips_a_paused_schedule()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (schedule, _, _) = await SeedDueScheduleAsync(fixture, new DateOnly(2026, 9, 1));
        schedule.Pause();
        fixture.Context.RecurringSchedules.Update(schedule);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GenerateDueRecurringDocumentsCommandHandler(fixture.Context, new FakeDateTime(Today), new FakeEmailSender(), new FakePublicLinkBuilder());

        var generatedCount = await handler.Handle(new GenerateDueRecurringDocumentsCommand(), CancellationToken.None);

        generatedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_generates_exactly_one_document_and_skips_past_several_missed_periods()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        // Three months stale (e.g. the schedule sat paused, or the app was down) —
        // should still only generate one document for "now," not backfill three.
        var (schedule, _, _) = await SeedDueScheduleAsync(fixture, new DateOnly(2026, 6, 1));

        var handler = new GenerateDueRecurringDocumentsCommandHandler(fixture.Context, new FakeDateTime(Today), new FakeEmailSender(), new FakePublicLinkBuilder());

        var generatedCount = await handler.Handle(new GenerateDueRecurringDocumentsCommand(), CancellationToken.None);

        generatedCount.Should().Be(1);
        (await fixture.Context.Documents.CountAsync()).Should().Be(1);

        var updatedSchedule = await fixture.Context.RecurringSchedules.SingleAsync(s => s.Id == schedule.Id);
        updatedSchedule.NextRunDate.Should().Be(new DateOnly(2026, 10, 1));
    }
}
