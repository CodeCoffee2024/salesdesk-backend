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
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));

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

    private static UpdateDocumentCommandHandler CreateHandler(SqliteApplicationDbContextFixture fixture, FakeCurrentUserService? currentUser = null) =>
        new(fixture.CreateContext(), fixture.Mapper, currentUser ?? CurrentUser, DateTime, new FakeEmailSender(), new FakePublicLinkBuilder());

    [Fact]
    public async Task Handle_updates_template_due_date_and_line_items_and_dispatches_when_requested()
    {
        var (fixture, documentId, otherTemplateId) = await SeedAsync();
        using var _1 = fixture;
        // A fresh context, not the seeding one — matches the per-request DbContext
        // lifetime the real API gives each handler call.
        var handler = CreateHandler(fixture);

        var command = new UpdateDocumentCommand(
            documentId,
            otherTemplateId,
            new DateOnly(2026, 9, 20),
            [new CreateDocumentLineItemRequest("Design review", 2m, 300m, null)],
            Dispatch: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.TemplateId.Should().Be(otherTemplateId);
        result.TemplateName.Should().Be("Modern Minimal");
        result.DueDate.Should().Be(new DateOnly(2026, 9, 20));
        result.Status.Should().Be(DocumentStatus.Sent);
        result.IsDispatched.Should().BeTrue();
        result.LineItems.Should().ContainSingle().Which.Description.Should().Be("Design review");
        result.Total.Should().Be(600m);
    }

    [Fact]
    public async Task Handle_leaves_status_as_draft_when_not_dispatching()
    {
        var (fixture, documentId, otherTemplateId) = await SeedAsync();
        using var _1 = fixture;
        var handler = CreateHandler(fixture);

        var command = new UpdateDocumentCommand(
            documentId, otherTemplateId, new DateOnly(2026, 9, 20),
            [new CreateDocumentLineItemRequest("Design review", 2m, 300m, null)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(DocumentStatus.Draft);
        result.IsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_leaves_currency_and_client_country_unchanged_when_not_provided()
    {
        var (fixture, documentId, otherTemplateId) = await SeedAsync();
        using var _1 = fixture;
        var handler = CreateHandler(fixture);

        var command = new UpdateDocumentCommand(
            documentId, otherTemplateId, new DateOnly(2026, 9, 20),
            [new CreateDocumentLineItemRequest("Design review", 2m, 300m, null)]);

        var result = await handler.Handle(command, CancellationToken.None);

        // Documents constructed directly (as SeedAsync does) default to USD/null.
        result.Currency.Should().Be("USD");
        result.ClientCountry.Should().BeNull();
    }

    [Fact]
    public async Task Handle_overrides_currency_and_client_country_when_provided()
    {
        var (fixture, documentId, otherTemplateId) = await SeedAsync();
        using var _1 = fixture;
        var handler = CreateHandler(fixture);

        var command = new UpdateDocumentCommand(
            documentId, otherTemplateId, new DateOnly(2026, 9, 20),
            [new CreateDocumentLineItemRequest("Design review", 2m, 300m, null)],
            Currency: "EUR",
            ClientCountry: "DE");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Currency.Should().Be("EUR");
        result.ClientCountry.Should().Be("DE");
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_document()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = CreateHandler(fixture);

        var command = new UpdateDocumentCommand(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 20), []);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_template()
    {
        var (fixture, documentId, _) = await SeedAsync();
        using var _1 = fixture;
        var handler = CreateHandler(fixture);

        var command = new UpdateDocumentCommand(
            documentId, Guid.NewGuid(), new DateOnly(2026, 9, 20), []);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_rejects_edits_to_a_dispatched_document()
    {
        var (fixture, documentId, otherTemplateId) = await SeedAsync();
        using var _1 = fixture;

        // First dispatch it (Draft -> Sent)...
        await CreateHandler(fixture).Handle(
            new UpdateDocumentCommand(documentId, otherTemplateId, new DateOnly(2026, 9, 20), [new CreateDocumentLineItemRequest("Design review", 2m, 300m, null)], Dispatch: true),
            CancellationToken.None);

        // ...then a second, ordinary edit attempt must be rejected (TASK-037
        // guardrail): a Sent document's content can't silently change under the
        // client without going through a revision first.
        var act = () => CreateHandler(fixture).Handle(
            new UpdateDocumentCommand(documentId, otherTemplateId, new DateOnly(2026, 9, 25), [new CreateDocumentLineItemRequest("Design review v2", 2m, 300m, null)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
