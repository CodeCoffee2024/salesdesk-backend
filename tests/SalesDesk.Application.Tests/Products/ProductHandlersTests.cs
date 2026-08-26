using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Products;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Products;

public class ProductHandlersTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    [Fact]
    public async Task GetProducts_returns_all_products_ordered_by_name()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        fixture.Context.Products.AddRange(
            new Product(WorkspaceId, "Web design & build", 6800m, ProductUnit.Project),
            new Product(WorkspaceId, "Art direction", 950m, ProductUnit.Day));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetProductsQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(new GetProductsQuery(), CancellationToken.None);

        result.Select(p => p.Name).Should().ContainInOrder("Art direction", "Web design & build");
    }

    [Fact]
    public async Task CreateProduct_persists_and_returns_the_new_product()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new CreateProductCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(
            new CreateProductCommand("SEO Audit", 750m, ProductUnit.Project, null, null),
            CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        (await fixture.Context.Products.CountAsync(CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateProduct_changes_the_editable_fields()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var product = new Product(WorkspaceId, "SEO Audit", 750m, ProductUnit.Project);
        fixture.Context.Products.Add(product);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateProductCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(
            new UpdateProductCommand(product.Id, "SEO Audit (extended)", 950m, ProductUnit.Day, "Now includes competitor analysis.", "Marketing"),
            CancellationToken.None);

        result.Name.Should().Be("SEO Audit (extended)");
        result.Price.Should().Be(950m);
        result.Unit.Should().Be(ProductUnit.Day);
    }

    [Fact]
    public async Task UpdateProduct_throws_NotFoundException_for_an_unknown_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new UpdateProductCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var act = () => handler.Handle(
            new UpdateProductCommand(Guid.NewGuid(), "SEO Audit", 750m, ProductUnit.Project, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteProduct_throws_NotFoundException_for_an_unknown_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new DeleteProductCommandHandler(fixture.Context, CurrentUser);

        var act = () => handler.Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteProduct_referenced_by_a_line_item_sets_the_line_items_product_id_to_null_instead_of_failing()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var product = new Product(WorkspaceId, "SEO Audit", 750m, ProductUnit.Project);
        var document = new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        var lineItem = document.AddLineItem("SEO Audit", 1m, 750m, product.Id);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Products.Add(product);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteProductCommandHandler(fixture.Context, CurrentUser);
        await handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        var reloadedLineItem = await fixture.Context.DocumentLineItems.FindAsync([lineItem.Id], CancellationToken.None);
        reloadedLineItem!.ProductId.Should().BeNull();
    }
}
