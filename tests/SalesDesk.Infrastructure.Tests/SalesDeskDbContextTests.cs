using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Infrastructure.Tests;

public class SalesDeskDbContextTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    private static Customer NewCustomer() => new(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");

    private static Template NewTemplate() => new(WorkspaceId, "Studio Standard", isDefault: true);

    [Fact]
    public async Task Customer_round_trips_all_fields_through_a_fresh_context()
    {
        using var fixture = new SqliteDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio", "+1 415 555 0100");

        fixture.Context.Customers.Add(customer);
        await fixture.Context.SaveChangesAsync();

        using var freshContext = fixture.CreateContext();
        var reloaded = await freshContext.Customers.FindAsync(customer.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Name.Should().Be("Maya Chen");
        reloaded.Company.Should().Be("Northstar Studio");
        reloaded.Email.Should().Be("maya@northstar.studio");
        reloaded.Phone.Should().Be("+1 415 555 0100");
        reloaded.CreatedAt.Should().BeCloseTo(customer.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Document_round_trips_its_line_items_and_computed_totals()
    {
        using var fixture = new SqliteDbContextFixture();
        var customer = NewCustomer();
        var template = NewTemplate();
        var document = new Document(WorkspaceId, "QUO-2026-101", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));
        document.AddLineItem("Research", 2m, 500m);
        document.AddLineItem("Design review", 1m, 300m);

        fixture.Context.AddRange(customer, template, document);
        await fixture.Context.SaveChangesAsync();

        using var freshContext = fixture.CreateContext();
        var reloaded = await freshContext.Documents
            .Include(d => d.LineItems)
            .SingleAsync(d => d.Id == document.Id);

        reloaded.Subtotal.Should().Be(1300m);
        reloaded.Total.Should().Be(1300m);
        reloaded.LineItems.Should().HaveCount(2);
        reloaded.LineItems.Should().Contain(li => li.Description == "Research" && li.LineTotal == 1000m);
        reloaded.LineItems.Should().Contain(li => li.Description == "Design review" && li.LineTotal == 300m);
    }

    [Fact]
    public async Task Enum_properties_persist_and_reload_by_name()
    {
        using var fixture = new SqliteDbContextFixture();
        var product = new Product(WorkspaceId, "Art direction", 950m, ProductUnit.Day);
        var template = new Template(WorkspaceId, "Friendly Quote", TemplateTargetType.QuotesOnly);

        fixture.Context.AddRange(product, template);
        await fixture.Context.SaveChangesAsync();

        using var freshContext = fixture.CreateContext();

        (await freshContext.Products.FindAsync(product.Id))!.Unit.Should().Be(ProductUnit.Day);
        (await freshContext.Templates.FindAsync(template.Id))!.TargetType.Should().Be(TemplateTargetType.QuotesOnly);
    }

    [Fact]
    public async Task Document_number_must_be_unique()
    {
        using var fixture = new SqliteDbContextFixture();
        var customer = NewCustomer();
        var template = NewTemplate();
        var first = new Document(WorkspaceId, "QUO-2026-100", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));
        first.AddLineItem("Research", 1m, 500m);

        fixture.Context.AddRange(customer, template, first);
        await fixture.Context.SaveChangesAsync();

        using var secondContext = fixture.CreateContext();
        var duplicate = new Document(WorkspaceId, "QUO-2026-100", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 16));
        duplicate.AddLineItem("Design", 1m, 300m);
        secondContext.Documents.Add(duplicate);

        var act = async () => await secondContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Deleting_a_document_cascades_delete_of_its_line_items()
    {
        using var fixture = new SqliteDbContextFixture();
        var customer = NewCustomer();
        var template = NewTemplate();
        var document = new Document(WorkspaceId, "QUO-2026-102", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));
        var lineItem = document.AddLineItem("Research", 1m, 500m);

        fixture.Context.AddRange(customer, template, document);
        await fixture.Context.SaveChangesAsync();

        using (var deleteContext = fixture.CreateContext())
        {
            var toDelete = await deleteContext.Documents.FindAsync(document.Id);
            deleteContext.Documents.Remove(toDelete!);
            await deleteContext.SaveChangesAsync();
        }

        using var verifyContext = fixture.CreateContext();
        (await verifyContext.Documents.FindAsync(document.Id)).Should().BeNull();
        (await verifyContext.DocumentLineItems.FindAsync(lineItem.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Deleting_a_customer_referenced_by_a_document_is_restricted()
    {
        using var fixture = new SqliteDbContextFixture();
        var customer = NewCustomer();
        var template = NewTemplate();
        var document = new Document(WorkspaceId, "QUO-2026-103", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));
        document.AddLineItem("Research", 1m, 500m);

        fixture.Context.AddRange(customer, template, document);
        await fixture.Context.SaveChangesAsync();

        using var deleteContext = fixture.CreateContext();
        var toDelete = await deleteContext.Customers.FindAsync(customer.Id);
        deleteContext.Customers.Remove(toDelete!);

        var act = async () => await deleteContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Deleting_a_template_referenced_by_a_document_is_restricted()
    {
        using var fixture = new SqliteDbContextFixture();
        var customer = NewCustomer();
        var template = NewTemplate();
        var document = new Document(WorkspaceId, "QUO-2026-104", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));
        document.AddLineItem("Research", 1m, 500m);

        fixture.Context.AddRange(customer, template, document);
        await fixture.Context.SaveChangesAsync();

        using var deleteContext = fixture.CreateContext();
        var toDelete = await deleteContext.Templates.FindAsync(template.Id);
        deleteContext.Templates.Remove(toDelete!);

        var act = async () => await deleteContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Deleting_a_product_referenced_by_a_line_item_sets_the_line_items_product_id_to_null()
    {
        using var fixture = new SqliteDbContextFixture();
        var customer = NewCustomer();
        var template = NewTemplate();
        var product = new Product(WorkspaceId, "SEO Audit", 750m, ProductUnit.Project);
        var document = new Document(WorkspaceId, "QUO-2026-105", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));
        var lineItem = document.AddLineItem("SEO Audit", 1m, 750m, product.Id);

        fixture.Context.AddRange(customer, template, product, document);
        await fixture.Context.SaveChangesAsync();

        using (var deleteContext = fixture.CreateContext())
        {
            var toDelete = await deleteContext.Products.FindAsync(product.Id);
            deleteContext.Products.Remove(toDelete!);
            await deleteContext.SaveChangesAsync();
        }

        using var verifyContext = fixture.CreateContext();
        var reloadedLineItem = await verifyContext.DocumentLineItems.FindAsync(lineItem.Id);

        reloadedLineItem.Should().NotBeNull();
        reloadedLineItem!.ProductId.Should().BeNull();
    }
}
