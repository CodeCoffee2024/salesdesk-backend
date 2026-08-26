using FluentAssertions;
using SalesDesk.Application.Admin;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Tests.Auth;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;
using SalesDesk.Infrastructure.Services;

namespace SalesDesk.Application.Tests.Admin;

public class AdminUsersHandlersTests
{
    private static readonly PasswordHasher PasswordHasher = new();
    private static readonly FakeTokenService TokenService = new();

    [Fact]
    public async Task GetUsers_returns_users_across_every_workspace_with_workspace_name()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var northline = new Workspace("Northline", "hello@northline.studio");
        var driftwood = new Workspace("Driftwood Studio", "hello@driftwoodstudio.com");
        fixture.Context.Workspaces.AddRange(northline, driftwood);
        fixture.Context.Users.AddRange(
            new User("jordan@northline.studio", "hash", "Jordan Reyes", Role.WorkspaceAdmin, northline.Id),
            new User("sam@driftwoodstudio.com", "hash", "Sam Diaz", Role.SalesManager, driftwood.Id));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetUsersQueryHandler(fixture.Context);
        var result = await handler.Handle(new GetUsersQuery(null, null), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(u => u.Email == "jordan@northline.studio" && u.WorkspaceName == "Northline");
        result.Should().ContainSingle(u => u.Email == "sam@driftwoodstudio.com" && u.WorkspaceName == "Driftwood Studio");
    }

    [Fact]
    public async Task GetUsers_filters_by_search_term_across_email_and_name()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Users.AddRange(
            new User("jordan@northline.studio", "hash", "Jordan Reyes", Role.WorkspaceAdmin, workspace.Id),
            new User("priya@northline.studio", "hash", "Priya Nair", Role.Viewer, workspace.Id));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetUsersQueryHandler(fixture.Context);
        var result = await handler.Handle(new GetUsersQuery("priya", null), CancellationToken.None);

        result.Should().ContainSingle(u => u.Email == "priya@northline.studio");
    }

    [Fact]
    public async Task GetUsers_filters_by_workspaceId()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var northline = new Workspace("Northline", "hello@northline.studio");
        var driftwood = new Workspace("Driftwood Studio", "hello@driftwoodstudio.com");
        fixture.Context.Workspaces.AddRange(northline, driftwood);
        fixture.Context.Users.AddRange(
            new User("jordan@northline.studio", "hash", "Jordan Reyes", Role.WorkspaceAdmin, northline.Id),
            new User("sam@driftwoodstudio.com", "hash", "Sam Diaz", Role.SalesManager, driftwood.Id));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetUsersQueryHandler(fixture.Context);
        var result = await handler.Handle(new GetUsersQuery(null, driftwood.Id), CancellationToken.None);

        result.Should().ContainSingle(u => u.Email == "sam@driftwoodstudio.com");
    }

    [Fact]
    public async Task Impersonate_issues_a_token_for_the_target_user_and_writes_an_audit_entry()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        var admin = new User("superadmin@salesdesk.app", "hash", "Sam Rivera", Role.SystemAdmin, workspace.Id);
        var target = new User("jordan@northline.studio", "hash", "Jordan Reyes", Role.WorkspaceAdmin, workspace.Id);
        fixture.Context.Users.AddRange(admin, target);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var auditLogger = new FakeAuditLogger();
        var currentUser = new FakeCurrentUserService(workspace.Id, admin.Id, Role.SystemAdmin);
        var handler = new ImpersonateUserCommandHandler(fixture.Context, TokenService, fixture.Mapper, auditLogger, currentUser);

        var result = await handler.Handle(new ImpersonateUserCommand(target.Id), CancellationToken.None);

        result.User.Email.Should().Be("jordan@northline.studio");
        result.Token.Should().NotBeNullOrEmpty();
        auditLogger.Entries.Should().ContainSingle(e =>
            e.EventType == AuditEventTypes.UserImpersonationStarted && e.UserId == target.Id && e.WorkspaceId == workspace.Id);
    }

    [Fact]
    public async Task Impersonate_rejects_impersonating_a_SystemAdmin()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        var admin = new User("superadmin@salesdesk.app", "hash", "Sam Rivera", Role.SystemAdmin, workspace.Id);
        var otherAdmin = new User("other-admin@salesdesk.app", "hash", "Alex Kim", Role.SystemAdmin, workspace.Id);
        fixture.Context.Users.AddRange(admin, otherAdmin);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var currentUser = new FakeCurrentUserService(workspace.Id, admin.Id, Role.SystemAdmin);
        var handler = new ImpersonateUserCommandHandler(fixture.Context, TokenService, fixture.Mapper, new FakeAuditLogger(), currentUser);

        var act = () => handler.Handle(new ImpersonateUserCommand(otherAdmin.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Impersonate_throws_NotFoundException_for_an_unknown_user()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var currentUser = new FakeCurrentUserService(Guid.NewGuid(), role: Role.SystemAdmin);
        var handler = new ImpersonateUserCommandHandler(fixture.Context, TokenService, fixture.Mapper, new FakeAuditLogger(), currentUser);

        var act = () => handler.Handle(new ImpersonateUserCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
