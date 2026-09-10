using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Documents;
using SalesDesk.Application.Notifications;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Tests.Documents;

public class RecordDocumentPaymentCommandHandlerTests
{
    private static async Task<(SqliteApplicationDbContextFixture Fixture, Document Document)> SeedInvoiceAsync()
    {
        var fixture = new SqliteApplicationDbContextFixture();
        var workspace = new Workspace("Northline", "hello@northline.studio");
        var owner = new User("owner@northline.studio", "hash", "Jordan Reyes", Role.WorkspaceAdmin, workspace.Id);
        var customer = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var template = new Template(workspace.Id, "Studio Standard", isDefault: true);
        var document = new Document(workspace.Id, "INV-2026-012", DocumentType.Invoice, customer.Id, template.Id, new DateOnly(2026, 8, 25), new DateOnly(2026, 9, 8));
        document.AddLineItem("Consulting", 1m, 1250m);

        fixture.Context.Workspaces.Add(workspace);
        fixture.Context.Users.Add(owner);
        fixture.Context.Customers.Add(customer);
        fixture.Context.Templates.Add(template);
        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        fixture.Context.PushSubscriptions.Add(new PushSubscription(owner.Id, "https://push.example/ep-1", "p256dh-key", "auth-key"));
        await fixture.Context.SaveChangesAsync(CancellationToken.None);

        return (fixture, document);
    }

    private static RecordDocumentPaymentCommandHandler MakeHandler(SqliteApplicationDbContextFixture fixture, FakePushNotificationSender pushSender, FakeEmailSender emailSender) =>
        new(fixture.CreateContext(), new WorkspacePushNotifier(fixture.CreateContext(), pushSender), new FakePublicLinkBuilder(), emailSender);

    [Fact]
    public async Task Handle_marks_the_invoice_paid_and_notifies_the_workspace_once()
    {
        var (fixture, document) = await SeedInvoiceAsync();
        var pushSender = new FakePushNotificationSender();
        var emailSender = new FakeEmailSender();
        var paidAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        await MakeHandler(fixture, pushSender, emailSender)
            .Handle(new RecordDocumentPaymentCommand(document.Id, 1250m, "USD", "cs_test_001", paidAt), CancellationToken.None);

        var persisted = await fixture.CreateContext().Documents.FirstAsync(d => d.Id == document.Id, CancellationToken.None);
        persisted.PaymentStatus.Should().Be(PaymentStatus.Paid);
        persisted.PaidAmount.Should().Be(1250m);
        persisted.Status.Should().Be(DocumentStatus.Paid);

        pushSender.SentNotifications.Should().ContainSingle(n => n.Title.Contains(document.DocumentNumber));
        emailSender.SentMessages.Should().ContainSingle(m => m.To == "hello@northline.studio" && m.Subject.Contains(document.DocumentNumber));
    }

    [Fact]
    public async Task Handle_is_idempotent_on_a_retried_webhook_delivery()
    {
        var (fixture, document) = await SeedInvoiceAsync();
        var pushSender = new FakePushNotificationSender();
        var emailSender = new FakeEmailSender();
        var paidAt = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        // Simulates Stripe redelivering the same event after the first delivery
        // already succeeded — must not double-charge state, double-notify, or throw.
        await MakeHandler(fixture, pushSender, emailSender)
            .Handle(new RecordDocumentPaymentCommand(document.Id, 1250m, "USD", "cs_test_001", paidAt), CancellationToken.None);
        await MakeHandler(fixture, pushSender, emailSender)
            .Handle(new RecordDocumentPaymentCommand(document.Id, 1250m, "USD", "cs_test_001", paidAt), CancellationToken.None);

        pushSender.SentNotifications.Should().HaveCount(1);
        emailSender.SentMessages.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_throws_NotFoundException_for_an_unknown_document_id()
    {
        using var fixture = new SqliteApplicationDbContextFixture();
        var handler = new RecordDocumentPaymentCommandHandler(
            fixture.Context, new WorkspacePushNotifier(fixture.Context, new FakePushNotificationSender()), new FakePublicLinkBuilder(), new FakeEmailSender());

        var act = () => handler.Handle(new RecordDocumentPaymentCommand(Guid.NewGuid(), 100m, "USD", "cs_test_missing", DateTime.UtcNow), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
