using FluentAssertions;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Documents;

public class UpdateDocumentStatusCommandHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

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

        var handler = new UpdateDocumentStatusCommandHandler(fixture.CreateContext(), fixture.Mapper, CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());

        var result = await handler.Handle(new UpdateDocumentStatusCommand(document.Id, DocumentStatus.Sent), CancellationToken.None);

        result.Status.Should().Be(DocumentStatus.Sent);
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_document()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new UpdateDocumentStatusCommandHandler(fixture.Context, fixture.Mapper, CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());

        var act = () => handler.Handle(new UpdateDocumentStatusCommand(Guid.NewGuid(), DocumentStatus.Paid), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
