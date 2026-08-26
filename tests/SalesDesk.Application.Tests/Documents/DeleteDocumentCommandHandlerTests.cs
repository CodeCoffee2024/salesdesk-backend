using FluentAssertions;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Documents;

public class DeleteDocumentCommandHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    [Fact]
    public async Task Handle_removes_the_document_and_cascades_its_line_items()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var document = new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        var lineItem = document.AddLineItem("Research", 1m, 500m);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteDocumentCommandHandler(fixture.Context, CurrentUser);

        await handler.Handle(new DeleteDocumentCommand(document.Id), CancellationToken.None);

        (await fixture.Context.Documents.FindAsync([document.Id], CancellationToken.None)).Should().BeNull();
        (await fixture.Context.DocumentLineItems.FindAsync([lineItem.Id], CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_document()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new DeleteDocumentCommandHandler(fixture.Context, CurrentUser);

        var act = () => handler.Handle(new DeleteDocumentCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
