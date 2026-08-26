using FluentAssertions;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Documents;

public class ConvertQuoteToInvoiceCommandHandlerTests
{
    private static readonly DateTimeOffset Today = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    private static async Task<(SqliteApplicationDbContextFixture Fixture, Document Quote)> SeedAcceptedQuoteAsync()
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var quote = new Document(WorkspaceId, "QUO-2026-027", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 7, 24), new DateOnly(2026, 8, 7));
        quote.AddLineItem("Art direction", 3m, 950m);
        quote.ChangeStatus(DocumentStatus.Accepted);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(quote);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        return (fixture, quote);
    }

    [Fact]
    public async Task Handle_creates_a_new_invoice_carrying_the_quotes_customer_template_and_line_items()
    {
        var (fixture, quote) = await SeedAcceptedQuoteAsync();
        using var _1 = fixture;
        var handler = new ConvertQuoteToInvoiceCommandHandler(fixture.CreateContext(), fixture.Mapper, new FakeDateTime(Today), CurrentUser);

        var invoice = await handler.Handle(new ConvertQuoteToInvoiceCommand(quote.Id), CancellationToken.None);

        invoice.Id.Should().NotBe(quote.Id);
        invoice.Type.Should().Be(DocumentType.Invoice);
        invoice.Status.Should().Be(DocumentStatus.Draft);
        invoice.DocumentNumber.Should().StartWith("INV-2026-");
        invoice.CustomerId.Should().Be(quote.CustomerId);
        invoice.TemplateId.Should().Be(quote.TemplateId);
        invoice.Total.Should().Be(2850m);
        invoice.LineItems.Should().ContainSingle().Which.Description.Should().Be("Art direction");
    }

    [Fact]
    public async Task Handle_leaves_the_original_quote_untouched()
    {
        var (fixture, quote) = await SeedAcceptedQuoteAsync();
        using var _1 = fixture;
        var handler = new ConvertQuoteToInvoiceCommandHandler(fixture.CreateContext(), fixture.Mapper, new FakeDateTime(Today), CurrentUser);

        await handler.Handle(new ConvertQuoteToInvoiceCommand(quote.Id), CancellationToken.None);

        using var verify = fixture.CreateContext();
        var reloadedQuote = await verify.Documents.FindAsync([quote.Id], CancellationToken.None);
        reloadedQuote!.Status.Should().Be(DocumentStatus.Accepted);
        reloadedQuote.Type.Should().Be(DocumentType.Quote);
    }

    [Fact]
    public async Task Handle_throws_for_a_quote_that_is_not_yet_accepted()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var quote = new Document(WorkspaceId, "QUO-2026-028", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 24));
        quote.AddLineItem("Web design", 1m, 6800m);
        quote.ChangeStatus(DocumentStatus.Sent);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(quote);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new ConvertQuoteToInvoiceCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);

        var act = () => handler.Handle(new ConvertQuoteToInvoiceCommand(quote.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_throws_for_a_document_that_is_already_an_invoice()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var invoice = new Document(WorkspaceId, "INV-2026-014", DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 26));
        invoice.AddLineItem("Brand identity sprint", 1m, 4200m);
        invoice.ChangeStatus(DocumentStatus.Paid);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(invoice);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new ConvertQuoteToInvoiceCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);

        var act = () => handler.Handle(new ConvertQuoteToInvoiceCommand(invoice.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_quote()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new ConvertQuoteToInvoiceCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);

        var act = () => handler.Handle(new ConvertQuoteToInvoiceCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
