using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Customers;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Customers;

public class CustomerHandlersTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    [Fact]
    public async Task GetCustomers_returns_all_customers_ordered_by_name()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        fixture.Context.Customers.AddRange(
            new Customer(WorkspaceId, "Priya Nair", "Goodform Labs", "priya@goodform.io"),
            new Customer(WorkspaceId, "Andre Santos", "Santos & Co.", "andre@santosco.ph"));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCustomersQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(new GetCustomersQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(c => c.Name).Should().ContainInOrder("Andre Santos", "Priya Nair");
    }

    [Fact]
    public async Task GetCustomers_computes_lifetime_value_from_paid_invoices_only()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);

        var paidInvoice = new Document(WorkspaceId, "INV-2026-001", DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        paidInvoice.AddLineItem("Retainer", 1m, 2400m);
        paidInvoice.ChangeStatus(DocumentStatus.Paid);

        var sentInvoice = new Document(WorkspaceId, "INV-2026-002", DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15));
        sentInvoice.AddLineItem("Retainer", 1m, 900m);
        sentInvoice.ChangeStatus(DocumentStatus.Sent);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.AddRange(paidInvoice, sentInvoice);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCustomersQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(new GetCustomersQuery(), CancellationToken.None);

        result.Single().LifetimeValue.Should().Be(2400m);
    }

    [Fact]
    public async Task CreateCustomer_persists_and_returns_the_new_customer()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new CreateCustomerCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(
            new CreateCustomerCommand("Maya Chen", "Northstar Studio", "maya@northstar.studio", "+1 415 555 0100"),
            CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        (await fixture.Context.Customers.CountAsync(CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateCustomer_changes_the_editable_fields()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        fixture.Context.Customers.Add(customer);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateCustomerCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(
            new UpdateCustomerCommand(customer.Id, "Maya C. Chen", "Northstar Studio LLC", "maya@northstarstudio.com", null),
            CancellationToken.None);

        result.Name.Should().Be("Maya C. Chen");
        result.Company.Should().Be("Northstar Studio LLC");
    }

    [Fact]
    public async Task CreateCustomer_persists_the_provided_country()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new CreateCustomerCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(
            new CreateCustomerCommand("Priya Nair", "Goodform Labs", "priya@goodform.io", null, "IN"),
            CancellationToken.None);

        result.Country.Should().Be("IN");
    }

    [Fact]
    public async Task UpdateCustomer_changes_the_country()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        fixture.Context.Customers.Add(customer);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateCustomerCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(
            new UpdateCustomerCommand(customer.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio", null, "PH"),
            CancellationToken.None);

        result.Country.Should().Be("PH");
    }

    [Fact]
    public async Task UpdateCustomer_throws_NotFoundException_for_an_unknown_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new UpdateCustomerCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var act = () => handler.Handle(
            new UpdateCustomerCommand(Guid.NewGuid(), "Maya Chen", "Northstar Studio", "maya@northstar.studio", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteCustomer_removes_the_customer()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        fixture.Context.Customers.Add(customer);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteCustomerCommandHandler(fixture.Context, CurrentUser);
        await handler.Handle(new DeleteCustomerCommand(customer.Id), CancellationToken.None);

        (await fixture.Context.Customers.FindAsync([customer.Id], CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteCustomer_throws_NotFoundException_for_an_unknown_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new DeleteCustomerCommandHandler(fixture.Context, CurrentUser);

        var act = () => handler.Handle(new DeleteCustomerCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteCustomer_referenced_by_a_document_throws_DbUpdateException()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var document = new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        document.AddLineItem("Research", 1m, 500m);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        // A fresh context: the Document referencing this customer isn't tracked
        // here, so the restriction is enforced by the database (DbUpdateException)
        // rather than caught client-side by EF's change tracker first — matching
        // what a real per-request handler call would see.
        var handler = new DeleteCustomerCommandHandler(fixture.CreateContext(), CurrentUser);
        var act = () => handler.Handle(new DeleteCustomerCommand(customer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
