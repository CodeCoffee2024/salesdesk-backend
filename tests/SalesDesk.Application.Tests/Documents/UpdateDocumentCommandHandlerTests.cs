using FluentAssertions;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Documents;

public class UpdateDocumentCommandHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    private static async Task<(SqliteApplicationDbContextFixture Fixture, Guid DocumentId, Guid OtherTemplateId)> SeedAsync()
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var otherTemplate = new Template(WorkspaceId, "Modern Minimal");
        var document = new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        document.AddLineItem("Research", 1m, 500m);

        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.AddRange(template, otherTemplate);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        return (fixture, document.Id, otherTemplate.Id);
    }

    [Fact]
    public async Task Handle_updates_template_due_date_status_and_line_items()
    {
        var (fixture, documentId, otherTemplateId) = await SeedAsync();
        using var _1 = fixture;
        // A fresh context, not the seeding one — matches the per-request DbContext
        // lifetime the real API gives each handler call.
        var handler = new UpdateDocumentCommandHandler(fixture.CreateContext(), fixture.Mapper, CurrentUser);

        var command = new UpdateDocumentCommand(
            documentId,
            otherTemplateId,
            new DateOnly(2026, 9, 20),
            DocumentStatus.Sent,
            [new CreateDocumentLineItemRequest("Design review", 2m, 300m, null)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.TemplateId.Should().Be(otherTemplateId);
        result.TemplateName.Should().Be("Modern Minimal");
        result.DueDate.Should().Be(new DateOnly(2026, 9, 20));
        result.Status.Should().Be(DocumentStatus.Sent);
        result.LineItems.Should().ContainSingle().Which.Description.Should().Be("Design review");
        result.Total.Should().Be(600m);
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_document()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new UpdateDocumentCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var command = new UpdateDocumentCommand(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 20), DocumentStatus.Sent, []);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_template()
    {
        var (fixture, documentId, _) = await SeedAsync();
        using var _1 = fixture;
        var handler = new UpdateDocumentCommandHandler(fixture.CreateContext(), fixture.Mapper, CurrentUser);

        var command = new UpdateDocumentCommand(
            documentId, Guid.NewGuid(), new DateOnly(2026, 9, 20), DocumentStatus.Sent, []);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
