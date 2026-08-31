using FluentAssertions;
using SalesDesk.Application.Documents;
using SalesDesk.Application.Documents.Public;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Documents;

/// <summary>
/// TASK-024's guardrail ("no modifications after e-signature") lives on
/// <see cref="Document.EnsureNotLocked"/>, but that check only works if the
/// handler's query actually loaded the <see cref="Document.Signature"/> navigation
/// first — omit the Include and the check silently passes against a null
/// navigation. DocumentTests (Domain) proves the guardrail logic itself is correct;
/// this class proves each handler's query shape actually wires it up, which a
/// domain-only test can't catch since it never goes through EF Core at all.
/// </summary>
public class DocumentLockingTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));

    private static async Task<(SqliteApplicationDbContextFixture Fixture, FakeCurrentUserService CurrentUser, Guid DocumentId, Guid TemplateId)> SeedSignedDocumentAsync()
    {
        var fixture = new SqliteApplicationDbContextFixture();
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

        var signHandler = new SignDocumentCommandHandler(fixture.CreateContext(), DateTime);
        await signHandler.Handle(
            new SignDocumentCommand(document.PublicToken, "Maya Chen", "maya@northstar.studio", true, SignatureType.Drawn, "data:image/png;base64,abc==", "203.0.113.5", "Mozilla/5.0"),
            CancellationToken.None);

        return (fixture, new FakeCurrentUserService(workspace.Id), document.Id, template.Id);
    }

    [Fact]
    public async Task DeleteDocumentCommandHandler_rejects_a_signed_document()
    {
        var (fixture, currentUser, documentId, _) = await SeedSignedDocumentAsync();
        using var _1 = fixture;
        var handler = new DeleteDocumentCommandHandler(fixture.CreateContext(), currentUser);

        var act = () => handler.Handle(new DeleteDocumentCommand(documentId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await fixture.CreateContext().Documents.FindAsync([documentId], CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateDocumentStatusCommandHandler_rejects_a_signed_document()
    {
        var (fixture, currentUser, documentId, _) = await SeedSignedDocumentAsync();
        using var _1 = fixture;
        var handler = new UpdateDocumentStatusCommandHandler(fixture.CreateContext(), fixture.Mapper, currentUser);

        var act = () => handler.Handle(new UpdateDocumentStatusCommand(documentId, DocumentStatus.Paid), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateDocumentCommandHandler_rejects_a_signed_document()
    {
        var (fixture, currentUser, documentId, templateId) = await SeedSignedDocumentAsync();
        using var _1 = fixture;
        var handler = new UpdateDocumentCommandHandler(fixture.CreateContext(), fixture.Mapper, currentUser);

        var command = new UpdateDocumentCommand(documentId, templateId, new DateOnly(2026, 9, 20), DocumentStatus.Sent, []);
        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
