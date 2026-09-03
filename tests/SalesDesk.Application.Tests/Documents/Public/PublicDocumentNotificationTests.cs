using FluentAssertions;
using SalesDesk.Application.Documents.Public;
using SalesDesk.Application.Notifications;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Documents.Public;

public class PublicDocumentNotificationTests
{
    private static readonly FakeDateTime DateTime = new(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));

    private static async Task<(SqliteApplicationDbContextFixture Fixture, Guid WorkspaceId, Document Document)> SeedAsync()
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var owner = new User("owner@northline.studio", "hash", "Jordan Reyes", Role.WorkspaceAdmin, workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var document = new Document(workspace.Id, "QUO-2026-035", DocumentType.Quote, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        document.AddLineItem("Research", 1m, 500m);
        document.ChangeStatus(DocumentStatus.Sent);

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Users.Add(owner);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        fixture.Context.PushSubscriptions.Add(new PushSubscription(owner.Id, "https://push.example/ep-1", "p256dh-key", "auth-key"));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        return (fixture, workspace.Id, document);
    }

    private static GetPublicDocumentByTokenQueryHandler MakeViewHandler(SqliteApplicationDbContextFixture fixture, FakePushNotificationSender sender, FakeEmailSender? emailSender = null) =>
        new(fixture.CreateContext(), new WorkspacePushNotifier(fixture.CreateContext(), sender), new FakePublicLinkBuilder(), DateTime, emailSender ?? new FakeEmailSender());

    [Fact]
    public async Task Viewing_a_document_for_the_first_time_sends_one_push_notification()
    {
        var (fixture, _, document) = await SeedAsync();
        var sender = new FakePushNotificationSender();
        var handler = MakeViewHandler(fixture, sender);

        await handler.Handle(new GetPublicDocumentByTokenQuery(document.PublicToken), CancellationToken.None);

        sender.SentNotifications.Should().ContainSingle(n => n.Title.Contains(document.DocumentNumber));
    }

    [Fact]
    public async Task Viewing_a_document_for_the_first_time_also_emails_the_workspace()
    {
        var (fixture, _, document) = await SeedAsync();
        var emailSender = new FakeEmailSender();
        var handler = MakeViewHandler(fixture, new FakePushNotificationSender(), emailSender);

        await handler.Handle(new GetPublicDocumentByTokenQuery(document.PublicToken), CancellationToken.None);

        emailSender.SentMessages.Should().ContainSingle(m => m.To == "hello@northline.studio" && m.Subject.Contains(document.DocumentNumber));
    }

    [Fact]
    public async Task Viewing_a_document_again_does_not_send_a_second_notification()
    {
        var (fixture, _, document) = await SeedAsync();
        var sender = new FakePushNotificationSender();

        await MakeViewHandler(fixture, sender).Handle(new GetPublicDocumentByTokenQuery(document.PublicToken), CancellationToken.None);
        await MakeViewHandler(fixture, sender).Handle(new GetPublicDocumentByTokenQuery(document.PublicToken), CancellationToken.None);

        sender.SentNotifications.Should().HaveCount(1);
    }

    [Fact]
    public async Task Signing_sends_a_push_notification()
    {
        var (fixture, _, document) = await SeedAsync();
        var sender = new FakePushNotificationSender();
        var handler = new SignDocumentCommandHandler(
            fixture.CreateContext(), DateTime, new WorkspacePushNotifier(fixture.CreateContext(), sender), new FakePublicLinkBuilder(), new FakeEmailSender());

        await handler.Handle(
            new SignDocumentCommand(document.PublicToken, "Maya Chen", "maya@northstar.studio", true, SignatureType.Drawn, "data:image/png;base64,abc==", "203.0.113.5", "Mozilla/5.0"),
            CancellationToken.None);

        sender.SentNotifications.Should().ContainSingle(n => n.Title.Contains("signed"));
    }

    [Fact]
    public async Task Requesting_a_revision_updates_status_and_sends_a_push_notification()
    {
        var (fixture, _, document) = await SeedAsync();
        var sender = new FakePushNotificationSender();
        var handler = new RequestDocumentRevisionCommandHandler(
            fixture.CreateContext(), DateTime, new WorkspacePushNotifier(fixture.CreateContext(), sender), new FakePublicLinkBuilder(), new FakeEmailSender());

        var result = await handler.Handle(new RequestDocumentRevisionCommand(document.PublicToken, "Please change the color scheme."), CancellationToken.None);

        result.Status.Should().Be(DocumentStatus.RevisionRequested);
        sender.SentNotifications.Should().ContainSingle(n => n.Body.Contains("Please change the color scheme"));
    }
}
