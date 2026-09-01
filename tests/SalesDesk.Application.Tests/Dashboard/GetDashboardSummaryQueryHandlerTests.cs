using FluentAssertions;
using SalesDesk.Application.Dashboard;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Dashboard;

public class GetDashboardSummaryQueryHandlerTests
{
    // "Today" for every test in this class: firmly inside Q3 (Jul 1 - Sep 30) 2026.
    private static readonly DateTimeOffset Today = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    [Fact]
    public async Task Handle_computes_all_four_aggregates_from_seeded_documents()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customerA = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var customerB = new Customer(WorkspaceId, "Andre Santos", "Santos & Co.", "andre@santosco.ph");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);

        Document Doc(string number, DocumentType type, DocumentStatus status, Customer customer, DateOnly issueDate, decimal total)
        {
            var document = new Document(WorkspaceId, number, type, customer.Id, template.Id, issueDate, issueDate.AddDays(14));
            document.AddLineItem("Work", 1m, total);
            document.ChangeStatus(status);
            return document;
        }

        var paidThisYear = Doc("INV-2026-001", DocumentType.Invoice, DocumentStatus.Paid, customerA, new DateOnly(2026, 2, 1), 1000m);
        var paidLastYear = Doc("INV-2025-050", DocumentType.Invoice, DocumentStatus.Paid, customerA, new DateOnly(2025, 12, 1), 500m);
        var sentInvoice = Doc("INV-2026-002", DocumentType.Invoice, DocumentStatus.Sent, customerA, new DateOnly(2026, 8, 1), 300m);
        var overdueInvoice = Doc("INV-2026-003", DocumentType.Invoice, DocumentStatus.Overdue, customerB, new DateOnly(2026, 7, 15), 200m);
        var draftInvoice = Doc("INV-2026-004", DocumentType.Invoice, DocumentStatus.Draft, customerB, new DateOnly(2026, 8, 10), 999m);
        var draftQuote = Doc("QUO-2026-001", DocumentType.Quote, DocumentStatus.Draft, customerA, new DateOnly(2026, 8, 12), 150m);
        var sentQuote = Doc("QUO-2026-002", DocumentType.Quote, DocumentStatus.Sent, customerB, new DateOnly(2026, 8, 15), 250m);
        var acceptedQuote = Doc("QUO-2026-003", DocumentType.Quote, DocumentStatus.Accepted, customerA, new DateOnly(2026, 1, 1), 400m);

        fixture.Context.Customers.AddRange(customerA, customerB);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.AddRange(
            paidThisYear, paidLastYear, sentInvoice, overdueInvoice, draftInvoice, draftQuote, sentQuote, acceptedQuote);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetDashboardSummaryQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        // Only the Paid invoice issued in 2026 counts — the Paid invoice from 2025 doesn't.
        result.RevenueThisYear.Should().Be(1000m);
        // Sent + Overdue invoices; Draft invoices are excluded.
        result.Outstanding.Should().Be(500m);
        // Draft + Sent quotes; the Accepted quote is excluded (decision already made).
        result.QuotePipeline.Should().Be(400m);
        // Distinct customers with a document issued on/after the current quarter's
        // start (2026-07-01): customerA (sentInvoice, draftQuote) and customerB
        // (overdueInvoice, draftInvoice, sentQuote) — the pre-Q3 documents don't add
        // a third customer since both customers already qualify via a Q3 document.
        result.ActiveCustomers.Should().Be(2);
        // No Workspace row seeded in this test, so the handler falls back to USD.
        result.BaseCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task Handle_returns_zeroes_when_there_are_no_documents()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new GetDashboardSummaryQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        result.RevenueThisYear.Should().Be(0m);
        result.Outstanding.Should().Be(0m);
        result.QuotePipeline.Should().Be(0m);
        result.ActiveCustomers.Should().Be(0);
    }

    [Fact]
    public async Task Handle_converts_foreign_currency_documents_into_the_workspaces_base_currency()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "US", defaultCurrency: "USD");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);

        var usdInvoice = new Document(workspace.Id, "INV-2026-001", DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15), currency: "USD");
        usdInvoice.AddLineItem("Work", 1m, 100m);
        usdInvoice.ChangeStatus(DocumentStatus.Paid);

        var eurInvoice = new Document(workspace.Id, "INV-2026-002", DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 2, 5), new DateOnly(2026, 2, 20), currency: "EUR");
        eurInvoice.AddLineItem("Work", 1m, 100m);
        eurInvoice.ChangeStatus(DocumentStatus.Paid);

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.AddRange(usdInvoice, eurInvoice);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var conversion = new FakeCurrencyConversionService();
        conversion.Rates["EUR-USD"] = 1.10m; // 100 EUR -> 110 USD

        var handler = new GetDashboardSummaryQueryHandler(fixture.Context, new FakeDateTime(Today), scopedCurrentUser, conversion);
        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        result.BaseCurrency.Should().Be("USD");
        result.RevenueThisYear.Should().Be(210m); // 100 USD + (100 EUR converted to 110 USD)
    }
}
