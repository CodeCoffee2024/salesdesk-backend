using FluentAssertions;
using SalesDesk.Application.Workspaces;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Workspaces;

public class WorkspaceBillingHandlerTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Get_returns_Free_with_no_end_date_for_a_standard_workspace()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetWorkspaceBillingQueryHandler(fixture.CreateContext(), new FakeCurrentUserService(workspace.Id), DateTime);
        var result = await handler.Handle(new GetWorkspaceBillingQuery(), CancellationToken.None);

        result.SubscriptionTier.Should().Be(nameof(SubscriptionTier.Free));
        result.SubscriptionEndDate.Should().BeNull();
        result.IsEarlyBirdPromo.Should().BeFalse();
        result.MonthlyDocumentLimit.Should().Be(5);
        result.DocumentsIssuedThisMonth.Should().Be(0);
    }

    [Fact]
    public async Task Get_returns_Pro_and_the_expiration_date_for_an_early_bird_workspace_with_no_document_limit()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var registeredAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        workspace.GrantEarlyBirdPro(registeredAt);
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetWorkspaceBillingQueryHandler(fixture.CreateContext(), new FakeCurrentUserService(workspace.Id), DateTime);
        var result = await handler.Handle(new GetWorkspaceBillingQuery(), CancellationToken.None);

        result.SubscriptionTier.Should().Be(nameof(SubscriptionTier.Pro));
        result.SubscriptionEndDate.Should().Be(registeredAt.AddDays(365));
        result.IsEarlyBirdPromo.Should().BeTrue();
        result.MonthlyDocumentLimit.Should().BeNull();
    }

    [Fact]
    public async Task Get_counts_only_documents_issued_in_the_current_calendar_month()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);

        var inMonth = new Document(workspace.Id, "QUO-2026-001", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 17));
        var alsoInMonth = new Document(workspace.Id, "QUO-2026-002", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 28));
        var lastMonth = new Document(workspace.Id, "QUO-2026-003", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 20), new DateOnly(2026, 9, 3));
        fixture.Context.Documents.AddRange(inMonth, alsoInMonth, lastMonth);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetWorkspaceBillingQueryHandler(fixture.CreateContext(), new FakeCurrentUserService(workspace.Id), DateTime);
        var result = await handler.Handle(new GetWorkspaceBillingQuery(), CancellationToken.None);

        result.DocumentsIssuedThisMonth.Should().Be(2);
    }
}
