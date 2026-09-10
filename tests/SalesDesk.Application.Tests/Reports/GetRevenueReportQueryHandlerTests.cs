using FluentAssertions;
using SalesDesk.Application.Reports;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Reports;

public class GetRevenueReportQueryHandlerTests
{
    private static readonly DateTimeOffset Today = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    private static Document Doc(Customer customer, Guid templateId, string number, DocumentType type, DocumentStatus status, DateOnly issueDate, decimal total, string currency = "USD")
    {
        var document = new Document(customer.WorkspaceId, number, type, customer.Id, templateId, issueDate, issueDate.AddDays(14), currency: currency);
        document.AddLineItem("Work", 1m, total);
        document.ChangeStatus(status);
        return document;
    }

    [Fact]
    public async Task Handle_buckets_paid_invoices_by_month_and_excludes_non_paid_and_out_of_range()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);

        var paidJune = Doc(customer, template.Id, "INV-2026-001", DocumentType.Invoice, DocumentStatus.Paid, new DateOnly(2026, 6, 10), 1000m);
        var paidJuneAgain = Doc(customer, template.Id, "INV-2026-002", DocumentType.Invoice, DocumentStatus.Paid, new DateOnly(2026, 6, 20), 500m);
        var paidJuly = Doc(customer, template.Id, "INV-2026-003", DocumentType.Invoice, DocumentStatus.Paid, new DateOnly(2026, 7, 5), 300m);
        var sentInRange = Doc(customer, template.Id, "INV-2026-004", DocumentType.Invoice, DocumentStatus.Sent, new DateOnly(2026, 7, 10), 999m);
        var quoteInRange = Doc(customer, template.Id, "QUO-2026-001", DocumentType.Quote, DocumentStatus.Paid, new DateOnly(2026, 7, 12), 999m);
        var paidOutOfRange = Doc(customer, template.Id, "INV-2026-005", DocumentType.Invoice, DocumentStatus.Paid, new DateOnly(2026, 5, 1), 999m);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.AddRange(paidJune, paidJuneAgain, paidJuly, sentInRange, quoteInRange, paidOutOfRange);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetRevenueReportQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        // Wide enough (>62 days) to force monthly buckets rather than daily ones.
        var result = await handler.Handle(new GetRevenueReportQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 30)), CancellationToken.None);

        result.Points.Should().HaveCount(4);
        result.Points.Should().ContainSingle(p => p.Label == "Jun 2026" && p.Amount == 1500m);
        result.Points.Should().ContainSingle(p => p.Label == "Jul 2026" && p.Amount == 300m);
        result.Points.Should().ContainSingle(p => p.Label == "Aug 2026" && p.Amount == 0m);
        result.Points.Should().ContainSingle(p => p.Label == "Sep 2026" && p.Amount == 0m);
    }

    [Fact]
    public async Task Handle_uses_daily_buckets_for_a_short_range_and_zero_fills_gaps()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var paid = Doc(customer, template.Id, "INV-2026-001", DocumentType.Invoice, DocumentStatus.Paid, new DateOnly(2026, 8, 3), 250m);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(paid);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetRevenueReportQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetRevenueReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)), CancellationToken.None);

        // 5-day range -> one point per day, not one per month.
        result.Points.Should().HaveCount(5);
        result.Points.Select(p => p.Amount).Should().Equal(0m, 0m, 250m, 0m, 0m);
    }

    [Fact]
    public async Task Handle_computes_outstanding_from_sent_and_overdue_invoices_in_range_only()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);

        var sentInRange = Doc(customer, template.Id, "INV-2026-001", DocumentType.Invoice, DocumentStatus.Sent, new DateOnly(2026, 8, 5), 400m);
        var overdueInRange = Doc(customer, template.Id, "INV-2026-002", DocumentType.Invoice, DocumentStatus.Overdue, new DateOnly(2026, 8, 10), 100m);
        var draftInRange = Doc(customer, template.Id, "INV-2026-003", DocumentType.Invoice, DocumentStatus.Draft, new DateOnly(2026, 8, 12), 999m);
        var sentOutOfRange = Doc(customer, template.Id, "INV-2026-004", DocumentType.Invoice, DocumentStatus.Sent, new DateOnly(2026, 1, 1), 999m);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.AddRange(sentInRange, overdueInRange, draftInRange, sentOutOfRange);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetRevenueReportQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetRevenueReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Outstanding.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_converts_foreign_currency_documents_into_the_workspaces_base_currency()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "US", defaultCurrency: "USD");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);

        var usdInvoice = Doc(customer, template.Id, "INV-2026-001", DocumentType.Invoice, DocumentStatus.Paid, new DateOnly(2026, 8, 3), 100m, "USD");
        var eurInvoice = Doc(customer, template.Id, "INV-2026-002", DocumentType.Invoice, DocumentStatus.Paid, new DateOnly(2026, 8, 5), 100m, "EUR");

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.AddRange(usdInvoice, eurInvoice);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var conversion = new FakeCurrencyConversionService();
        conversion.Rates["EUR-USD"] = 1.10m;

        var handler = new GetRevenueReportQueryHandler(fixture.Context, new FakeDateTime(Today), scopedCurrentUser, conversion);
        var result = await handler.Handle(new GetRevenueReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.BaseCurrency.Should().Be("USD");
        result.Points.Sum(p => p.Amount).Should().Be(210m);
    }

    [Fact]
    public async Task Handle_returns_zero_filled_points_and_zero_outstanding_when_no_documents_in_range()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new GetRevenueReportQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetRevenueReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Points.Should().OnlyContain(p => p.Amount == 0m);
        result.Outstanding.Should().Be(0m);
    }
}
