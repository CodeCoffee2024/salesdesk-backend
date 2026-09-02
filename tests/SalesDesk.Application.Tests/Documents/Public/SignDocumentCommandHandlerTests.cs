using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Documents.Public;
using SalesDesk.Application.Notifications;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Documents.Public;

public class SignDocumentCommandHandlerTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));

    private static SignDocumentCommand ValidCommand(Guid token) => new(
        token, "Jane Client", "jane@example.com", AgreedToTerms: true,
        SignatureType.Drawn, "data:image/png;base64,abc==", "203.0.113.5", "Mozilla/5.0");

    private static SignDocumentCommandHandler MakeHandler(IApplicationDbContext context) =>
        new(context, DateTime, new WorkspacePushNotifier(context, new FakePushNotificationSender()), new FakePublicLinkBuilder(), new FakeEmailSender());

    [Fact]
    public async Task Handle_persists_the_signature_and_locks_the_document()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var document = new Document(workspace.Id, "QUO-2026-035", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        document.AddLineItem("Research", 1m, 500m);

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = MakeHandler(fixture.CreateContext());
        var result = await handler.Handle(ValidCommand(document.PublicToken), CancellationToken.None);

        result.IsSigned.Should().BeTrue();
        result.SignedByName.Should().Be("Jane Client");
        result.Status.Should().Be(DocumentStatus.Accepted);

        // Re-fetched through a brand new context (not the one the handler wrote
        // through) so this actually proves SaveChanges committed a real row, not
        // just that the in-memory graph looks right — this is the check that
        // would have caught the signature being issued as an UPDATE instead of an
        // INSERT (DocumentSignature.Id is a client-generated Guid, so it isn't
        // distinguishable from an existing row unless explicitly Add()-ed).
        var persistedDocument = await fixture.CreateContext().Documents
            .Include(d => d.Signature)
            .FirstAsync(d => d.Id == document.Id, CancellationToken.None);

        persistedDocument.Signature.Should().NotBeNull();
        persistedDocument.Signature!.SignerName.Should().Be("Jane Client");
        persistedDocument.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_token()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = MakeHandler(fixture.Context);

        var act = () => handler.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_throws_when_the_document_is_already_signed()
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

        var firstHandler = MakeHandler(fixture.CreateContext());
        await firstHandler.Handle(ValidCommand(document.PublicToken), CancellationToken.None);

        var secondHandler = MakeHandler(fixture.CreateContext());
        var act = () => secondHandler.Handle(ValidCommand(document.PublicToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
