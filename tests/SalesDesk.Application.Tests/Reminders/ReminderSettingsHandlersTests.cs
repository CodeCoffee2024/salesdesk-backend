using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Reminders;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Reminders;

public class ReminderSettingsHandlersTests
{
    [Fact]
    public async Task Get_returns_disabled_defaults_when_no_row_exists_yet()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var currentUser = new FakeCurrentUserService(Guid.NewGuid());

        var result = await new GetReminderSettingsQueryHandler(fixture.CreateContext(), currentUser)
            .Handle(new GetReminderSettingsQuery(), CancellationToken.None);

        result.IsEnabled.Should().BeFalse();
        result.QuoteFollowUpEnabled.Should().BeTrue();
        result.InvoiceDueWarningEnabled.Should().BeTrue();
        result.OverdueNoticesEnabled.Should().BeTrue();
        result.CcEmail.Should().BeNull();
    }

    [Fact]
    public async Task Save_creates_a_row_on_first_save_and_updates_it_on_the_next()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        fixture.Context.Workspaces.Add(workspace);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        var currentUser = new FakeCurrentUserService(workspace.Id);
        var handler = new SaveReminderSettingsCommandHandler(fixture.CreateContext(), currentUser);

        var created = await handler.Handle(
            new SaveReminderSettingsCommand(true, true, false, true, "owner@northline.studio"), CancellationToken.None);
        created.IsEnabled.Should().BeTrue();
        created.InvoiceDueWarningEnabled.Should().BeFalse();
        created.CcEmail.Should().Be("owner@northline.studio");

        var updated = await handler.Handle(
            new SaveReminderSettingsCommand(false, true, true, true, null), CancellationToken.None);
        updated.IsEnabled.Should().BeFalse();
        updated.InvoiceDueWarningEnabled.Should().BeTrue();
        updated.CcEmail.Should().BeNull();

        (await fixture.CreateContext().ReminderSettingsEntries.CountAsync(CancellationToken.None))
            .Should().Be(1);
    }
}
