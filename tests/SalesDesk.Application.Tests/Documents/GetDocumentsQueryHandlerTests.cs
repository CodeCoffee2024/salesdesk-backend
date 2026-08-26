using FluentAssertions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Documents;

public class GetDocumentsQueryHandlerTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    private static Document Doc(string number, DocumentType type, DocumentStatus status, Customer customer, Template template)
    {
        var document = new Document(WorkspaceId, number, type, customer.Id, template.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));
        document.AddLineItem("Work", 1m, 500m);
        document.ChangeStatus(status);
        return document;
    }

    private static async Task<(SqliteApplicationDbContextFixture Fixture, Customer Maya, Customer Andre, Template Template)> SeedAsync()
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var maya = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var andre = new Customer(WorkspaceId, "Andre Santos", "Santos & Co.", "andre@santosco.ph");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);

        var quoteDraft = Doc("QUO-2026-001", DocumentType.Quote, DocumentStatus.Draft, maya, template);
        var quoteSent = Doc("QUO-2026-002", DocumentType.Quote, DocumentStatus.Sent, andre, template);
        var invoicePaid = Doc("INV-2026-001", DocumentType.Invoice, DocumentStatus.Paid, maya, template);

        fixture.Context.Customers.AddRange(maya, andre);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.AddRange(quoteDraft, quoteSent, invoicePaid);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        return (fixture, maya, andre, template);
    }

    [Fact]
    public async Task Handle_with_no_filters_returns_every_document()
    {
        var (fixture, _, _, _) = await SeedAsync();
        using var _1 = fixture;
        var handler = new GetDocumentsQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(new GetDocumentsQuery(null, null, null), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("all", 3)]
    [InlineData("quote", 2)]
    [InlineData("invoice", 1)]
    public async Task Handle_filters_by_type(string type, int expectedCount)
    {
        var (fixture, _, _, _) = await SeedAsync();
        using var _1 = fixture;
        var handler = new GetDocumentsQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(new GetDocumentsQuery(type, null, null), CancellationToken.None);

        result.Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task Handle_filters_by_status_case_insensitively()
    {
        var (fixture, _, _, _) = await SeedAsync();
        using var _1 = fixture;
        var handler = new GetDocumentsQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(new GetDocumentsQuery(null, "SENT", null), CancellationToken.None);

        result.Should().ContainSingle().Which.DocumentNumber.Should().Be("QUO-2026-002");
    }

    [Fact]
    public async Task Handle_searches_by_document_number()
    {
        var (fixture, _, _, _) = await SeedAsync();
        using var _1 = fixture;
        var handler = new GetDocumentsQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(new GetDocumentsQuery(null, null, "inv-2026"), CancellationToken.None);

        result.Should().ContainSingle().Which.DocumentNumber.Should().Be("INV-2026-001");
    }

    [Fact]
    public async Task Handle_searches_by_customer_name_case_insensitively()
    {
        var (fixture, _, _, _) = await SeedAsync();
        using var _1 = fixture;
        var handler = new GetDocumentsQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(new GetDocumentsQuery(null, null, "andre"), CancellationToken.None);

        result.Should().ContainSingle().Which.CustomerName.Should().Be("Andre Santos");
    }

    [Fact]
    public async Task Handle_populates_customer_and_template_names_and_line_items()
    {
        var (fixture, _, _, _) = await SeedAsync();
        using var _1 = fixture;
        var handler = new GetDocumentsQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(new GetDocumentsQuery("invoice", null, null), CancellationToken.None);

        var invoice = result.Should().ContainSingle().Subject;
        invoice.CustomerName.Should().Be("Maya Chen");
        invoice.CustomerCompany.Should().Be("Northstar Studio");
        invoice.TemplateName.Should().Be("Studio Standard");
        invoice.LineItems.Should().ContainSingle().Which.LineTotal.Should().Be(500m);
    }
}
