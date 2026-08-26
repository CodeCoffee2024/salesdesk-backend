using FluentAssertions;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Documents;

public class CreateDocumentCommandHandlerTests
{
    private static readonly DateTimeOffset Today = new(2026, 8, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    [Fact]
    public async Task Handle_creates_a_draft_document_numbered_for_the_current_year_and_type()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);
        var command = new CreateDocumentCommand(
            DocumentType.Quote,
            customer.Id,
            template.Id,
            new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Research", 2m, 500m, null)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.DocumentNumber.Should().Be("QUO-2026-001");
        result.Status.Should().Be(DocumentStatus.Draft);
        result.IssueDate.Should().Be(new DateOnly(2026, 8, 25));
        result.Total.Should().Be(1000m);
        result.LineItems.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_numbers_sequentially_within_the_same_type_and_year()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);
        var command = new CreateDocumentCommand(
            DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        first.DocumentNumber.Should().Be("INV-2026-001");
        second.DocumentNumber.Should().Be("INV-2026-002");
    }

    [Fact]
    public async Task Handle_increments_the_templates_usage_count()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        await handler.Handle(command, CancellationToken.None);

        template.UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_customer()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);
        var command = new CreateDocumentCommand(
            DocumentType.Quote, Guid.NewGuid(), template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_template()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        fixture.Context.Customers.Add(customer);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, Guid.NewGuid(), new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_rejects_a_line_item_with_zero_quantity()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser);
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 0m, 100m, null)]);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
