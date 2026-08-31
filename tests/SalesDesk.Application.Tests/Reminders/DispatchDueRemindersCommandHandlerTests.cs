using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Reminders;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Reminders;

public class DispatchDueRemindersCommandHandlerTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Today = new(2026, 8, 31);

    private static (Workspace Workspace, Customer Customer, Template Template) SeedWorkspace(SqliteApplicationDbContextFixture fixture, bool remindersEnabled = true, string? ccEmail = "owner@northline.studio")
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var settings = new ReminderSettings(workspace.Id, remindersEnabled, quoteFollowUpEnabled: true, invoiceDueWarningEnabled: true, overdueNoticesEnabled: true, ccEmail: ccEmail);

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.ReminderSettingsEntries.Add(settings);
        fixture.Context.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();

        return (workspace, customer, template);
    }

    private static Document AddDocument(
        SqliteApplicationDbContextFixture fixture, Workspace workspace, Customer customer, Template template,
        string number, DocumentType type, DateOnly issueDate, DateOnly dueDate, DocumentStatus status)
    {
        var document = new Document(workspace.Id, number, type, customer.Id, template.Id, issueDate, dueDate);
        document.AddLineItem("Work", 1m, 1000m);
        document.ChangeStatus(status);

        fixture.Context.Documents.Add(document);
        fixture.Context.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
        return document;
    }

    private static DispatchDueRemindersCommandHandler CreateHandler(SqliteApplicationDbContextFixture fixture, FakeEmailSender emailSender) =>
        new(fixture.CreateContext(), emailSender, new FakePublicLinkBuilder(), DateTime);

    [Fact]
    public async Task Handle_sends_quote_follow_up_exactly_3_days_after_issue()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture);
        var document = AddDocument(fixture, workspace, customer, template, "QUO-1", DocumentType.Quote, Today.AddDays(-3), Today.AddDays(11), DocumentStatus.Sent);

        var emailSender = new FakeEmailSender();
        var sentCount = await CreateHandler(fixture, emailSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        sentCount.Should().Be(1);
        emailSender.SentMessages.Should().ContainSingle();
        emailSender.SentMessages[0].To.Should().Be(customer.Email);
        emailSender.SentMessages[0].Cc.Should().Be("owner@northline.studio");

        var log = await fixture.CreateContext().DocumentReminderLogs.SingleAsync(l => l.DocumentId == document.Id, CancellationToken.None);
        log.Type.Should().Be(ReminderType.QuoteFollowUp);
    }

    [Fact]
    public async Task Handle_does_not_send_when_reminders_are_disabled_for_the_workspace()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture, remindersEnabled: false);
        AddDocument(fixture, workspace, customer, template, "QUO-1", DocumentType.Quote, Today.AddDays(-3), Today.AddDays(11), DocumentStatus.Sent);

        var emailSender = new FakeEmailSender();
        var sentCount = await CreateHandler(fixture, emailSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        sentCount.Should().Be(0);
        emailSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_never_sends_the_same_reminder_twice_for_the_same_document()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture);
        AddDocument(fixture, workspace, customer, template, "QUO-1", DocumentType.Quote, Today.AddDays(-5), Today.AddDays(11), DocumentStatus.Sent);

        var firstRunSender = new FakeEmailSender();
        var firstRunCount = await CreateHandler(fixture, firstRunSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        var secondRunSender = new FakeEmailSender();
        var secondRunCount = await CreateHandler(fixture, secondRunSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        firstRunCount.Should().Be(1);
        secondRunCount.Should().Be(0);
        secondRunSender.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_sends_invoice_due_soon_within_the_2_day_window()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture);
        var document = AddDocument(fixture, workspace, customer, template, "INV-1", DocumentType.Invoice, Today.AddDays(-10), Today.AddDays(2), DocumentStatus.Sent);

        var emailSender = new FakeEmailSender();
        var sentCount = await CreateHandler(fixture, emailSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        sentCount.Should().Be(1);
        var log = await fixture.CreateContext().DocumentReminderLogs.SingleAsync(l => l.DocumentId == document.Id, CancellationToken.None);
        log.Type.Should().Be(ReminderType.InvoiceDueSoon);
    }

    [Fact]
    public async Task Handle_does_not_send_invoice_due_soon_more_than_2_days_before_due()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture);
        AddDocument(fixture, workspace, customer, template, "INV-1", DocumentType.Invoice, Today.AddDays(-1), Today.AddDays(5), DocumentStatus.Sent);

        var emailSender = new FakeEmailSender();
        var sentCount = await CreateHandler(fixture, emailSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        sentCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_sends_first_overdue_notice_1_day_past_due()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture);
        var document = AddDocument(fixture, workspace, customer, template, "INV-1", DocumentType.Invoice, Today.AddDays(-20), Today.AddDays(-1), DocumentStatus.Sent);

        var emailSender = new FakeEmailSender();
        await CreateHandler(fixture, emailSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        var log = await fixture.CreateContext().DocumentReminderLogs.SingleAsync(l => l.DocumentId == document.Id, CancellationToken.None);
        log.Type.Should().Be(ReminderType.InvoiceOverdueFirstNotice);
    }

    [Fact]
    public async Task Handle_sends_final_overdue_notice_7_days_past_due_even_if_the_first_notice_was_never_logged()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture);
        var document = AddDocument(fixture, workspace, customer, template, "INV-1", DocumentType.Invoice, Today.AddDays(-30), Today.AddDays(-7), DocumentStatus.Sent);

        var emailSender = new FakeEmailSender();
        var sentCount = await CreateHandler(fixture, emailSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        sentCount.Should().Be(1);
        var logs = await fixture.CreateContext().DocumentReminderLogs.Where(l => l.DocumentId == document.Id).ToListAsync(CancellationToken.None);
        logs.Should().ContainSingle(l => l.Type == ReminderType.InvoiceOverdueFinalNotice);
    }

    [Fact]
    public async Task Handle_suppresses_a_second_reminder_to_the_same_customer_within_24_hours()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture);

        // Two independent documents for the same customer, both eligible for a reminder today.
        AddDocument(fixture, workspace, customer, template, "QUO-1", DocumentType.Quote, Today.AddDays(-3), Today.AddDays(11), DocumentStatus.Sent);
        AddDocument(fixture, workspace, customer, template, "INV-1", DocumentType.Invoice, Today.AddDays(-10), Today.AddDays(1), DocumentStatus.Sent);

        var emailSender = new FakeEmailSender();
        var sentCount = await CreateHandler(fixture, emailSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        sentCount.Should().Be(1);
        emailSender.SentMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_does_not_send_for_a_quote_that_has_not_yet_reached_3_days()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (workspace, customer, template) = SeedWorkspace(fixture);
        AddDocument(fixture, workspace, customer, template, "QUO-1", DocumentType.Quote, Today.AddDays(-1), Today.AddDays(13), DocumentStatus.Sent);

        var emailSender = new FakeEmailSender();
        var sentCount = await CreateHandler(fixture, emailSender).Handle(new DispatchDueRemindersCommand(), CancellationToken.None);

        sentCount.Should().Be(0);
    }
}
