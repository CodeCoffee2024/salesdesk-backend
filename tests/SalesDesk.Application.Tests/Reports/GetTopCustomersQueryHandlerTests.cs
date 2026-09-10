using FluentAssertions;
using SalesDesk.Application.Reports;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Reports;

public class GetTopCustomersQueryHandlerTests
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
    public async Task Handle_ranks_top_5_customers_by_billed_total_excluding_drafts_and_quotes()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var customers = Enumerable.Range(1, 6)
            .Select(i => new Customer(WorkspaceId, $"Customer {i}", $"Company {i}", $"c{i}@example.com"))
            .ToArray();

        var documents = new List<Document>();
        for (var i = 0; i < customers.Length; i++)
        {
            // Billed totals: 600, 500, 400, 300, 200, 100 — customer 6 (100) should be cut by Take(5).
            documents.Add(Doc(customers[i], template.Id, $"INV-2026-{i:000}", DocumentType.Invoice, DocumentStatus.Sent, new DateOnly(2026, 8, 5), (6 - i) * 100m));
        }

        // Noise that must not count: a draft invoice (highest total of all) and a sent quote.
        documents.Add(Doc(customers[0], template.Id, "INV-2026-900", DocumentType.Invoice, DocumentStatus.Draft, new DateOnly(2026, 8, 6), 5000m));
        documents.Add(Doc(customers[0], template.Id, "QUO-2026-900", DocumentType.Quote, DocumentStatus.Sent, new DateOnly(2026, 8, 6), 5000m));

        fixture.Context.Customers.AddRange(customers);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.AddRange(documents);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTopCustomersQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetTopCustomersQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Should().HaveCount(5);
        result.Select(c => c.BilledTotal).Should().Equal(600m, 500m, 400m, 300m, 200m);
        result.First().Name.Should().Be("Customer 1");
        result.First().Company.Should().Be("Company 1");
    }

    [Fact]
    public async Task Handle_converts_foreign_currency_before_ranking()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "US", defaultCurrency: "USD");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var eurInvoice = Doc(customer, template.Id, "INV-2026-001", DocumentType.Invoice, DocumentStatus.Sent, new DateOnly(2026, 8, 5), 100m, "EUR");

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(eurInvoice);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var conversion = new FakeCurrencyConversionService();
        conversion.Rates["EUR-USD"] = 1.10m;

        var handler = new GetTopCustomersQueryHandler(fixture.Context, new FakeDateTime(Today), scopedCurrentUser, conversion);
        var result = await handler.Handle(new GetTopCustomersQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Should().ContainSingle().Which.BilledTotal.Should().Be(110m);
    }

    [Fact]
    public async Task Handle_returns_empty_list_when_no_documents_in_range()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new GetTopCustomersQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetTopCustomersQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
