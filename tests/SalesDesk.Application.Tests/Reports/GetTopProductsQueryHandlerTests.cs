using FluentAssertions;
using SalesDesk.Application.Reports;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Reports;

public class GetTopProductsQueryHandlerTests
{
    private static readonly DateTimeOffset Today = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    // Line items can only be added while a document is still Draft/RevisionRequested
    // (Document.EnsureEditable) — so build it Draft, add lines, then change status.
    private static Document NewDraftDocument(Customer customer, Guid templateId, string number, DocumentType type, DateOnly issueDate, string currency = "USD") =>
        new(customer.WorkspaceId, number, type, customer.Id, templateId, issueDate, issueDate.AddDays(14), currency: currency);

    [Fact]
    public async Task Handle_ranks_top_5_products_by_line_value_excluding_drafts_and_quotes()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var products = Enumerable.Range(1, 6)
            .Select(i => new Product(WorkspaceId, $"Product {i}", (7 - i) * 10m, ProductUnit.Project))
            .ToArray();

        var billed = NewDraftDocument(customer, template.Id, "INV-2026-001", DocumentType.Invoice, new DateOnly(2026, 8, 5));
        for (var i = 0; i < products.Length; i++)
        {
            // Line values: 600, 500, 400, 300, 200, 100 — product 6 (100) should be cut by Take(5).
            billed.AddLineItem(products[i].Name, 1m, (products.Length - i) * 100m, products[i].Id);
        }
        billed.ChangeStatus(DocumentStatus.Sent);

        var draft = NewDraftDocument(customer, template.Id, "INV-2026-002", DocumentType.Invoice, new DateOnly(2026, 8, 6));
        draft.AddLineItem(products[0].Name, 1m, 9999m, products[0].Id);

        var quote = NewDraftDocument(customer, template.Id, "QUO-2026-001", DocumentType.Quote, new DateOnly(2026, 8, 6));
        quote.AddLineItem(products[0].Name, 1m, 9999m, products[0].Id);
        quote.ChangeStatus(DocumentStatus.Sent);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Products.AddRange(products);
        fixture.Context.Documents.AddRange(billed, draft, quote);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTopProductsQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetTopProductsQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Should().HaveCount(5);
        result.Select(p => p.Amount).Should().Equal(600m, 500m, 400m, 300m, 200m);
        result.First().Name.Should().Be("Product 1");
    }

    [Fact]
    public async Task Handle_excludes_line_items_with_no_linked_product()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var product = new Product(WorkspaceId, "Real Product", 50m, ProductUnit.Project);

        var document = NewDraftDocument(customer, template.Id, "INV-2026-001", DocumentType.Invoice, new DateOnly(2026, 8, 5));
        // Free-text line, never linked to the catalog, with the highest total —
        // must not appear in results or otherwise displace the real product.
        document.AddLineItem("Custom one-off work", 1m, 9999m, productId: null);
        document.AddLineItem(product.Name, 1m, 200m, product.Id);
        document.ChangeStatus(DocumentStatus.Sent);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Products.Add(product);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTopProductsQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetTopProductsQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Name.Should().Be("Real Product");
        result.Single().Amount.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_converts_foreign_currency_before_ranking()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "US", defaultCurrency: "USD");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var product = new Product(workspace.Id, "Design sprint", 100m, ProductUnit.Project);

        var document = NewDraftDocument(customer, template.Id, "INV-2026-001", DocumentType.Invoice, new DateOnly(2026, 8, 5), "EUR");
        document.AddLineItem(product.Name, 1m, 100m, product.Id);
        document.ChangeStatus(DocumentStatus.Sent);

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Products.Add(product);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var conversion = new FakeCurrencyConversionService();
        conversion.Rates["EUR-USD"] = 1.10m;

        var handler = new GetTopProductsQueryHandler(fixture.Context, new FakeDateTime(Today), scopedCurrentUser, conversion);
        var result = await handler.Handle(new GetTopProductsQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Should().ContainSingle().Which.Amount.Should().Be(110m);
    }

    [Fact]
    public async Task Handle_returns_empty_list_when_no_documents_in_range()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new GetTopProductsQueryHandler(fixture.Context, new FakeDateTime(Today), CurrentUser, new FakeCurrencyConversionService());

        var result = await handler.Handle(new GetTopProductsQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
