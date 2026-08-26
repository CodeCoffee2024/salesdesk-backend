using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Templates;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Tests.Templates;

public class TemplateHandlersTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly FakeCurrentUserService CurrentUser = new(WorkspaceId);

    [Fact]
    public async Task GetTemplates_returns_all_templates_ordered_by_name()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        fixture.Context.Templates.AddRange(
            new Template(WorkspaceId, "Studio Standard", isDefault: true),
            new Template(WorkspaceId, "Friendly Quote", TemplateTargetType.QuotesOnly));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetTemplatesQueryHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(new GetTemplatesQuery(), CancellationToken.None);

        result.Select(t => t.Name).Should().ContainInOrder("Friendly Quote", "Studio Standard");
    }

    [Fact]
    public async Task CreateTemplate_persists_and_returns_the_new_template()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new CreateTemplateCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var result = await handler.Handle(
            new CreateTemplateCommand("Warm Proposal", TemplateTargetType.QuotesOnly, "For thoughtful project proposals.", "#D9A441"),
            CancellationToken.None);

        result.Id.Should().NotBeEmpty();
        result.IsDefault.Should().BeFalse();
        result.UsageCount.Should().Be(0);
        (await fixture.Context.Templates.CountAsync(CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateTemplate_changes_the_editable_fields()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var template = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        fixture.Context.Templates.Add(template);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateTemplateCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(
            new UpdateTemplateCommand(template.Id, "Studio Standard v2", TemplateTargetType.InvoicesOnly, "Updated layout.", "#2F6F6C"),
            CancellationToken.None);

        result.Name.Should().Be("Studio Standard v2");
        result.TargetType.Should().Be(TemplateTargetType.InvoicesOnly);
        // IsDefault isn't part of the editable field set — should be untouched.
        result.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTemplate_throws_NotFoundException_for_an_unknown_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new UpdateTemplateCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var act = () => handler.Handle(
            new UpdateTemplateCommand(Guid.NewGuid(), "Studio Standard", TemplateTargetType.QuotesAndInvoices, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteTemplate_throws_NotFoundException_for_an_unknown_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new DeleteTemplateCommandHandler(fixture.Context, CurrentUser);

        var act = () => handler.Handle(new DeleteTemplateCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetDefaultTemplate_marks_the_target_default_and_unmarks_the_previous_one()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var current = new Template(WorkspaceId, "Studio Standard", isDefault: true);
        var candidate = new Template(WorkspaceId, "Friendly Quote", TemplateTargetType.QuotesOnly);
        fixture.Context.Templates.AddRange(current, candidate);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new SetDefaultTemplateCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);
        var result = await handler.Handle(new SetDefaultTemplateCommand(candidate.Id), CancellationToken.None);

        result.IsDefault.Should().BeTrue();
        (await fixture.Context.Templates.FindAsync([current.Id], CancellationToken.None))!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultTemplate_throws_NotFoundException_for_an_unknown_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new SetDefaultTemplateCommandHandler(fixture.Context, fixture.Mapper, CurrentUser);

        var act = () => handler.Handle(new SetDefaultTemplateCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteTemplate_referenced_by_a_document_throws_DbUpdateException()
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

        // A fresh context: the Document referencing this template isn't tracked
        // here, so the restriction is enforced by the database (DbUpdateException)
        // rather than caught client-side by EF's change tracker first — matching
        // what a real per-request handler call would see.
        var handler = new DeleteTemplateCommandHandler(fixture.CreateContext(), CurrentUser);
        var act = () => handler.Handle(new DeleteTemplateCommand(template.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
