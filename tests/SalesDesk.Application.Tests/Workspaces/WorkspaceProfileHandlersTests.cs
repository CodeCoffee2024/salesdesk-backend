using FluentAssertions;
using FluentValidation;
using SalesDesk.Application.Workspaces;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Workspaces;

public class WorkspaceProfileHandlersTests
{
    [Fact]
    public async Task Get_returns_the_current_workspaces_profile()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio", tagline: "Creative studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new GetWorkspaceProfileQueryHandler(fixture.CreateContext(), new FakeCurrentUserService(workspace.Id));
        var result = await handler.Handle(new GetWorkspaceProfileQuery(), CancellationToken.None);

        result.Name.Should().Be("Northline");
        result.Tagline.Should().Be("Creative studio");
        result.LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task Update_persists_the_new_profile_fields()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateWorkspaceProfileCommandHandler(fixture.CreateContext(), new FakeCurrentUserService(workspace.Id));
        var result = await handler.Handle(
            new UpdateWorkspaceProfileCommand("Northline Studio", "hello@northline.studio", "Now with a tagline", "1 Main St", "https://cdn.example.com/logo.png"),
            CancellationToken.None);

        result.Name.Should().Be("Northline Studio");
        result.LogoUrl.Should().Be("https://cdn.example.com/logo.png");

        var persisted = fixture.CreateContext().Workspaces.Single(w => w.Id == workspace.Id);
        persisted.Name.Should().Be("Northline Studio");
        persisted.LogoUrl.Should().Be("https://cdn.example.com/logo.png");
    }

    [Fact]
    public void Validator_rejects_a_non_http_logo_url()
    {
        var validator = new UpdateWorkspaceProfileCommandValidator();
        var command = new UpdateWorkspaceProfileCommand("Northline", "hello@northline.studio", null, null, "not-a-url");

        var act = () => validator.ValidateAndThrow(command);

        act.Should().Throw<ValidationException>();
    }
}
