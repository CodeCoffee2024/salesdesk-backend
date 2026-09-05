using FluentAssertions;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

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

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
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

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
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

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
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

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
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

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, Guid.NewGuid(), new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_defaults_currency_to_USD_when_no_workspace_row_or_override_exists()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var customer = new Customer(WorkspaceId, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Currency.Should().Be("USD");
        result.ClientCountry.Should().BeNull();
    }

    [Fact]
    public async Task Handle_defaults_currency_and_client_country_from_the_workspace_and_customer()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "PH", defaultCurrency: "PHP");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Priya Nair", "Goodform Labs", "priya@goodform.io", country: "IN");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), scopedCurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        var result = await handler.Handle(command, CancellationToken.None);

        // The customer's own country wins over the workspace's operating country.
        result.Currency.Should().Be("PHP");
        result.ClientCountry.Should().Be("IN");
    }

    [Fact]
    public async Task Handle_uses_explicit_currency_and_client_country_overrides_when_provided()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "PH", defaultCurrency: "PHP");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Priya Nair", "Goodform Labs", "priya@goodform.io", country: "IN");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), scopedCurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)],
            Currency: "EUR",
            ClientCountry: "DE");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Currency.Should().Be("EUR");
        result.ClientCountry.Should().Be("DE");
    }

    [Fact]
    public async Task Handle_blocks_a_sixth_document_this_month_on_the_Free_tier()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio"); // Free tier by default — 5 documents/month.
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), scopedCurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        for (var i = 0; i < 5; i++)
        {
            await handler.Handle(command, CancellationToken.None);
        }

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<PlanLimitExceededException>();
    }

    [Fact]
    public async Task Handle_exempts_SystemAdmin_from_the_Free_tier_document_cap()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("SalesDesk HQ", "ops@salesdesk.app"); // Free tier by default, same as the seeded platform workspace.
        var systemAdmin = new FakeCurrentUserService(workspace.Id, role: Role.SystemAdmin);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), systemAdmin, new FakeEmailSender(), new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)]);

        for (var i = 0; i < 6; i++)
        {
            await handler.Handle(command, CancellationToken.None);
        }

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_dispatch_email_uses_the_documents_own_currency_symbol_not_the_servers_default()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", country: "PH", defaultCurrency: "PHP");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Priya Nair", "Goodform Labs", "priya@goodform.io");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), scopedCurrentUser, emailSender, new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 450m, null)],
            Dispatch: true);

        await handler.Handle(command, CancellationToken.None);

        emailSender.SentMessages.Should().ContainSingle();
        emailSender.SentMessages[0].HtmlBody.Should().Contain("₱450.00");
        emailSender.SentMessages[0].HtmlBody.Should().NotContain("$450.00");
    }

    [Fact]
    public async Task Handle_dispatch_email_includes_the_activity_timeline_so_far()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), scopedCurrentUser, emailSender, new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)],
            Dispatch: true);

        await handler.Handle(command, CancellationToken.None);

        var body = emailSender.SentMessages.Single().HtmlBody;
        body.Should().Contain("Activity so far");
        body.Should().Contain("Sent to you");
        // Created is drafting-only and must never reach the client's own inbox.
        body.Should().NotContain("Document created");
    }

    [Fact]
    public async Task Handle_dispatch_email_localizes_the_timeline_into_the_workspaces_time_zone()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", timeZoneId: "America/Los_Angeles");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), scopedCurrentUser, emailSender, new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)],
            Dispatch: true);

        await handler.Handle(command, CancellationToken.None);

        var body = emailSender.SentMessages.Single().HtmlBody;
        // Today is midnight UTC on Aug 25 — in America/Los_Angeles (UTC-7 in
        // August) that's 5:00 PM the day before, so this also proves real zone
        // conversion (including the date rolling back), not just a label swap.
        body.Should().Contain("Aug 24, 5:00 PM PDT");
        body.Should().NotContain("UTC");
    }

    [Fact]
    public async Task Handle_dispatch_email_includes_the_templates_body_with_merge_tags_resolved()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var scopedCurrentUser = new FakeCurrentUserService(workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(
            workspace.Id, "Studio Standard", isDefault: true,
            contentHtml: "<p>Thanks for working with us, {{Customer.Name}} — {{Document.Number}} is ready.</p>");
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var emailSender = new FakeEmailSender();
        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), scopedCurrentUser, emailSender, new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 1m, 100m, null)],
            Dispatch: true);

        var created = await handler.Handle(command, CancellationToken.None);

        var body = emailSender.SentMessages.Single().HtmlBody;
        body.Should().Contain($"Thanks for working with us, Maya Chen — {created.DocumentNumber} is ready.");
        body.Should().NotContain("{{");
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

        var handler = new CreateDocumentCommandHandler(fixture.Context, fixture.Mapper, new FakeDateTime(Today), CurrentUser, new FakeEmailSender(), new FakePublicLinkBuilder());
        var command = new CreateDocumentCommand(
            DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 8),
            [new CreateDocumentLineItemRequest("Work", 0m, 100m, null)]);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
