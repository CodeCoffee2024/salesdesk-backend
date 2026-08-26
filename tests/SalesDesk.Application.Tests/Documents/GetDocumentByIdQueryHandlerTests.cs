using FluentAssertions;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Documents;

public class GetDocumentByIdQueryHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    [Fact]
    public async Task Handle_returns_the_fully_populated_document()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var document = new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        document.AddLineItem("Research", 2m, 500m);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetDocumentByIdQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(new GetDocumentByIdQuery(document.Id), CancellationToken.None);

        result.DocumentNumber.Should().Be("QUO-2026-035");
        result.CustomerName.Should().Be("Maya Chen");
        result.TemplateName.Should().Be("Studio Standard");
        result.Total.Should().Be(1000m);
        result.LineItems.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new GetDocumentByIdQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var act = () => handler.Handle(new GetDocumentByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
