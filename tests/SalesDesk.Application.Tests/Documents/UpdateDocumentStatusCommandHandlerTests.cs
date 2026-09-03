using FluentAssertions;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Documents;

public class UpdateDocumentStatusCommandHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Handle_updates_the_status_and_returns_the_document()
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

        var handler = new UpdateDocumentStatusCommandHandler(fixture.CreateContext(), fixture.Mapper, CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder(), DateTime);

        var result = await handler.Handle(new UpdateDocumentStatusCommand(document.Id, DocumentStatus.Sent), CancellationToken.None);

        result.Status.Should().Be(DocumentStatus.Sent);
        result.IsDispatched.Should().BeTrue();
    }

    [Fact]
    public async Task Marking_an_invoice_paid_emails_the_customer_a_confirmation()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var currentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var document = new Document(workspace.Id, "INV-2026-011", DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        document.AddLineItem("Design work", 1m, 500m);
        document.ChangeStatus(DocumentStatus.Sent);

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var handler = new UpdateDocumentStatusCommandHandler(fixture.CreateContext(), fixture.Mapper, currentUser, emailSender, new FakePublicLinkBuilder(), DateTime);

        await handler.Handle(new UpdateDocumentStatusCommand(document.Id, DocumentStatus.Paid), CancellationToken.None);

        emailSender.SentMessages.Should().ContainSingle(m => m.To == customer.Email && m.Subject.Contains("Payment received"));
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_document()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new UpdateDocumentStatusCommandHandler(fixture.Context, fixture.Mapper, CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder(), DateTime);

        var act = () => handler.Handle(new UpdateDocumentStatusCommand(Guid.NewGuid(), DocumentStatus.Paid), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
