using FluentAssertions;
using SalesDesk.Application.Admin;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Admin;

public class AdminHandlersTests
{
    [Fact]
    public async Task GetPlatformMetrics_counts_workspaces_users_and_documents()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var active = new Workspace("Northline", "hello@northline.studio");
        var suspended = new Workspace("Driftwood Studio", "hello@driftwoodstudio.com");
        suspended.Suspend();
        fixture.Context.Workspaces.AddRange(active, suspended);
        fixture.Context.Users.Add(new User("admin@northline.studio", "hash", "Jordan Reyes", Role.WorkspaceAdmin, active.Id));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetPlatformMetricsQueryHandler(fixture.Context);
        var result = await handler.Handle(new GetPlatformMetricsQuery(), CancellationToken.None);

        result.TotalWorkspaces.Should().Be(2);
        result.TotalActiveWorkspaces.Should().Be(1);
        result.TotalUsers.Should().Be(1);
        result.SystemHealth.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetWorkspaces_filters_by_search_term_across_name_and_email()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        fixture.Context.Workspaces.AddRange(
            new Workspace("Northline", "hello@northline.studio"),
            new Workspace("Driftwood Studio", "hello@driftwoodstudio.com"));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetWorkspacesQueryHandler(fixture.Context);
        var result = await handler.Handle(new GetWorkspacesQuery("north"), CancellationToken.None);

        result.Should().ContainSingle(w => w.Name == "Northline");
    }

    [Fact]
    public async Task SetWorkspaceStatus_suspends_a_workspace_and_writes_an_audit_entry()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var auditLogger = new FakeAuditLogger();
        var admin = new FakeCurrentUserService(Guid.NewGuid(), role: Role.SystemAdmin);
        var handler = new SetWorkspaceStatusCommandHandler(fixture.Context, auditLogger, admin);

        var result = await handler.Handle(new SetWorkspaceStatusCommand(workspace.Id, IsActive: false), CancellationToken.None);

        result.IsActive.Should().BeFalse();
        auditLogger.Entries.Should().ContainSingle(e => e.EventType == AuditEventTypes.WorkspaceSuspended && e.WorkspaceId == workspace.Id);
    }

    [Fact]
    public async Task SetWorkspaceStatus_throws_NotFoundException_for_an_unknown_workspace()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new SetWorkspaceStatusCommandHandler(fixture.Context, new FakeAuditLogger(), new FakeCurrentUserService(Guid.NewGuid()));

        var act = () => handler.Handle(new SetWorkspaceStatusCommand(Guid.NewGuid(), IsActive: false), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetWorkspaceQuota_updates_the_quota_and_writes_an_audit_entry()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var auditLogger = new FakeAuditLogger();
        var handler = new SetWorkspaceQuotaCommandHandler(fixture.Context, auditLogger, new FakeCurrentUserService(Guid.NewGuid(), role: Role.SystemAdmin));

        var result = await handler.Handle(new SetWorkspaceQuotaCommand(workspace.Id, 250), CancellationToken.None);

        result.DocumentQuota.Should().Be(250);
        auditLogger.Entries.Should().ContainSingle(e => e.EventType == AuditEventTypes.WorkspaceQuotaChanged);
    }

    [Fact]
    public async Task GetAuditLog_returns_newest_entries_first()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        fixture.Context.AuditLogs.Add(new AuditLog(AuditEventTypes.WorkspaceRegistered, "First", null, null));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);
        fixture.Context.AuditLogs.Add(new AuditLog(AuditEventTypes.SystemError, "Second", null, null));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetAuditLogQueryHandler(fixture.Context);
        var result = await handler.Handle(new GetAuditLogQuery(Search: null), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Select(i => i.Message).Should().ContainInOrder("Second", "First");
    }
}
