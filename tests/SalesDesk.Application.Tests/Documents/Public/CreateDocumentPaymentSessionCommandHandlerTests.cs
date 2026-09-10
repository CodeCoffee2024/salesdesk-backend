using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Documents.Public;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Documents.Public;

public class CreateDocumentPaymentSessionCommandHandlerTests
{
    private static (Workspace Workspace, Customer Customer, Template Template, Document Document) SeedInvoice(IApplicationDbContext context)
    {
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var document = new Document(workspace.Id, "INV-2026-012", DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        document.AddLineItem("Consulting", 1m, 1250m);

        context.Workspaces.Add(workspace);
        context.Customers.Add(customer);
        context.Templates.Add(template);
        context.Documents.Add(document);

        return (workspace, customer, template, document);
    }

    [Fact]
    public async Task Handle_creates_a_session_and_persists_the_provider_reference()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (_, _, _, document) = SeedInvoice(fixture.Context);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var gateway = new FakeInvoicePaymentGatewayService();
        var handler = new CreateDocumentPaymentSessionCommandHandler(fixture.CreateContext(), gateway, new FakePublicLinkBuilder());

        var result = await handler.Handle(new CreateDocumentPaymentSessionCommand(document.PublicToken), CancellationToken.None);

        result.CheckoutUrl.Should().Be(gateway.CheckoutUrl);
        gateway.LastDocumentId.Should().Be(document.Id);

        var persisted = await fixture.CreateContext().Documents.FirstAsync(d => d.Id == document.Id, CancellationToken.None);
        persisted.StripeCheckoutSessionId.Should().Be(gateway.ProviderReference);
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_token()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new CreateDocumentPaymentSessionCommandHandler(fixture.Context, new FakeInvoicePaymentGatewayService(), new FakePublicLinkBuilder());

        var act = () => handler.Handle(new CreateDocumentPaymentSessionCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_throws_for_a_quote()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var document = new Document(workspace.Id, "QUO-2026-035", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentPaymentSessionCommandHandler(fixture.CreateContext(), new FakeInvoicePaymentGatewayService(), new FakePublicLinkBuilder());

        var act = () => handler.Handle(new CreateDocumentPaymentSessionCommand(document.PublicToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_throws_when_the_invoice_is_already_paid()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (_, _, _, document) = SeedInvoice(fixture.Context);
        document.RecordPayment(1250m, DateTime.UtcNow, "cs_test_already_paid");
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentPaymentSessionCommandHandler(fixture.CreateContext(), new FakeInvoicePaymentGatewayService(), new FakePublicLinkBuilder());

        var act = () => handler.Handle(new CreateDocumentPaymentSessionCommand(document.PublicToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_bubbles_PaymentGatewayUnavailableException_from_the_gateway()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var (_, _, _, document) = SeedInvoice(fixture.Context);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var gateway = new FakeInvoicePaymentGatewayService { ThrowUnavailable = true };
        var handler = new CreateDocumentPaymentSessionCommandHandler(fixture.CreateContext(), gateway, new FakePublicLinkBuilder());

        var act = () => handler.Handle(new CreateDocumentPaymentSessionCommand(document.PublicToken), CancellationToken.None);

        await act.Should().ThrowAsync<PaymentGatewayUnavailableException>();
    }
}
