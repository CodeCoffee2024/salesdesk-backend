using FluentAssertions;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Domain.Tests;

public class DocumentTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TemplateId = Guid.NewGuid();
    private static readonly DateOnly IssueDate = new(2026, 8, 25);
    private static readonly DateOnly DueDate = new(2026, 9, 8);

    private static Document CreateDocument(DocumentType type = DocumentType.Quote) =>
        new(WorkspaceId, "QUO-2026-035", type, CustomerId, TemplateId, IssueDate, DueDate);

    [Fact]
    public void Constructor_starts_in_Draft_status_with_no_line_items_and_zero_totals()
    {
        var document = CreateDocument();

        document.Status.Should().Be(DocumentStatus.Draft);
        document.LineItems.Should().BeEmpty();
        document.Subtotal.Should().Be(0m);
        document.Total.Should().Be(0m);
    }

    [Fact]
    public void Constructor_rejects_a_due_date_earlier_than_the_issue_date()
    {
        var act = () => new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, CustomerId, TemplateId, IssueDate, IssueDate.AddDays(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_rejects_a_blank_document_number()
    {
        var act = () => new Document(WorkspaceId, "", DocumentType.Quote, CustomerId, TemplateId, IssueDate, DueDate);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [MemberData(nameof(EmptyGuidCases))]
    public void Constructor_rejects_empty_customer_or_template_ids(Guid customerId, Guid templateId)
    {
        var act = () => new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, customerId, templateId, IssueDate, DueDate);

        act.Should().Throw<ArgumentException>();
    }

    public static IEnumerable<object[]> EmptyGuidCases()
    {
        yield return [Guid.Empty, TemplateId];
        yield return [CustomerId, Guid.Empty];
    }

    [Fact]
    public void AddLineItem_appends_the_item_and_recalculates_subtotal_and_total()
    {
        var document = CreateDocument();

        var lineItem = document.AddLineItem("Brand identity sprint", 1m, 4200m);

        document.LineItems.Should().ContainSingle().Which.Should().BeSameAs(lineItem);
        lineItem.LineTotal.Should().Be(4200m);
        document.Subtotal.Should().Be(4200m);
        document.Total.Should().Be(4200m);
    }

    [Fact]
    public void AddLineItem_sums_multiple_items_into_the_totals()
    {
        var document = CreateDocument();

        document.AddLineItem("Research", 2m, 500m);
        document.AddLineItem("Design review", 3m, 150m);

        document.LineItems.Should().HaveCount(2);
        document.Subtotal.Should().Be(1450m);
        document.Total.Should().Be(1450m);
    }

    [Fact]
    public void AddLineItem_links_the_line_item_to_this_document()
    {
        var document = CreateDocument();

        var lineItem = document.AddLineItem("Research", 1m, 500m);

        lineItem.DocumentId.Should().Be(document.Id);
    }

    [Fact]
    public void AddLineItem_rejects_zero_or_negative_quantity()
    {
        var document = CreateDocument();

        var act = () => document.AddLineItem("Research", 0m, 500m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddLineItem_rejects_a_negative_unit_price()
    {
        var document = CreateDocument();

        var act = () => document.AddLineItem("Research", 1m, -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateLineItem_changes_the_item_and_recalculates_totals()
    {
        var document = CreateDocument();
        var lineItem = document.AddLineItem("Research", 2m, 500m);

        document.UpdateLineItem(lineItem.Id, "Research (revised)", 3m, 600m);

        lineItem.Description.Should().Be("Research (revised)");
        lineItem.LineTotal.Should().Be(1800m);
        document.Subtotal.Should().Be(1800m);
        document.Total.Should().Be(1800m);
    }

    [Fact]
    public void UpdateLineItem_throws_for_a_line_item_id_that_does_not_belong_to_this_document()
    {
        var document = CreateDocument();

        var act = () => document.UpdateLineItem(Guid.NewGuid(), "Research", 1m, 500m);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveLineItem_removes_the_item_and_recalculates_totals()
    {
        var document = CreateDocument();
        var keep = document.AddLineItem("Research", 1m, 500m);
        var drop = document.AddLineItem("Design review", 1m, 300m);

        document.RemoveLineItem(drop.Id);

        document.LineItems.Should().ContainSingle().Which.Should().BeSameAs(keep);
        document.Subtotal.Should().Be(500m);
        document.Total.Should().Be(500m);
    }

    [Fact]
    public void RemoveLineItem_throws_for_a_line_item_id_that_does_not_belong_to_this_document()
    {
        var document = CreateDocument();

        var act = () => document.RemoveLineItem(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ChangeStatus_updates_the_status()
    {
        var document = CreateDocument();

        document.ChangeStatus(DocumentStatus.Sent);

        document.Status.Should().Be(DocumentStatus.Sent);
    }

    [Fact]
    public void Reschedule_updates_the_due_date()
    {
        var document = CreateDocument();

        document.Reschedule(DueDate.AddDays(10));

        document.DueDate.Should().Be(DueDate.AddDays(10));
    }

    [Fact]
    public void Reschedule_rejects_a_due_date_earlier_than_the_issue_date()
    {
        var document = CreateDocument();

        var act = () => document.Reschedule(IssueDate.AddDays(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChangeTemplate_updates_the_template_id()
    {
        var document = CreateDocument();
        var newTemplateId = Guid.NewGuid();

        document.ChangeTemplate(newTemplateId);

        document.TemplateId.Should().Be(newTemplateId);
    }

    [Fact]
    public void ChangeTemplate_rejects_an_empty_id()
    {
        var document = CreateDocument();

        var act = () => document.ChangeTemplate(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReplaceLineItems_swaps_the_entire_set_and_recalculates_totals()
    {
        var document = CreateDocument();
        document.AddLineItem("Old item", 1m, 100m);

        document.ReplaceLineItems([
            new NewLineItem("Research", 2m, 500m, null),
            new NewLineItem("Design review", 1m, 300m, null)
        ]);

        document.LineItems.Should().HaveCount(2);
        document.LineItems.Should().NotContain(li => li.Description == "Old item");
        document.Subtotal.Should().Be(1300m);
        document.Total.Should().Be(1300m);
    }

    [Fact]
    public void ReplaceLineItems_with_an_empty_set_zeroes_the_totals()
    {
        var document = CreateDocument();
        document.AddLineItem("Research", 1m, 500m);

        document.ReplaceLineItems([]);

        document.LineItems.Should().BeEmpty();
        document.Subtotal.Should().Be(0m);
        document.Total.Should().Be(0m);
    }

    [Fact]
    public void Constructor_defaults_currency_to_USD_with_no_client_country()
    {
        var document = CreateDocument();

        document.Currency.Should().Be("USD");
        document.ClientCountry.Should().BeNull();
    }

    [Fact]
    public void Constructor_normalizes_currency_and_client_country_to_uppercase()
    {
        var document = new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, CustomerId, TemplateId, IssueDate, DueDate, currency: "eur", clientCountry: "de");

        document.Currency.Should().Be("EUR");
        document.ClientCountry.Should().Be("DE");
    }

    [Fact]
    public void Constructor_rejects_a_malformed_currency_code()
    {
        var act = () => new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, CustomerId, TemplateId, IssueDate, DueDate, currency: "EU");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeCurrency_updates_currency_and_client_country()
    {
        var document = CreateDocument();

        document.ChangeCurrency("GBP", "GB");

        document.Currency.Should().Be("GBP");
        document.ClientCountry.Should().Be("GB");
    }

    [Fact]
    public void ChangeCurrency_allows_clearing_the_client_country_override()
    {
        var document = new Document(WorkspaceId, "QUO-2026-035", DocumentType.Quote, CustomerId, TemplateId, IssueDate, DueDate, clientCountry: "DE");

        document.ChangeCurrency("USD", null);

        document.ClientCountry.Should().BeNull();
    }

    [Fact]
    public void ComputeContentHash_changes_when_currency_changes()
    {
        var document = CreateDocument();
        document.AddLineItem("Research", 1m, 500m);
        var originalHash = document.ComputeContentHash();

        document.ChangeCurrency("EUR", null);

        document.ComputeContentHash().Should().NotBe(originalHash);
    }

    [Fact]
    public void Constructor_assigns_a_distinct_non_empty_public_token()
    {
        var first = CreateDocument();
        var second = CreateDocument();

        first.PublicToken.Should().NotBe(Guid.Empty);
        first.PublicToken.Should().NotBe(second.PublicToken);
    }

    [Fact]
    public void ApplySignature_locks_the_document_and_transitions_it_to_Accepted()
    {
        var document = CreateDocument();
        document.AddLineItem("Research", 1m, 500m);

        var signature = document.ApplySignature(
            "Jane Client", "jane@example.com", SignatureType.Drawn, "data:image/png;base64,abc==",
            "203.0.113.5", "Mozilla/5.0", DateTimeOffset.UtcNow);

        document.IsLocked.Should().BeTrue();
        document.Status.Should().Be(DocumentStatus.Accepted);
        document.Signature.Should().BeSameAs(signature);
        signature.DocumentHash.Should().Be(document.ComputeContentHash());
    }

    [Fact]
    public void ApplySignature_throws_when_the_document_is_already_signed()
    {
        var document = CreateDocument();
        document.ApplySignature(
            "Jane Client", "jane@example.com", SignatureType.Typed, "data:image/png;base64,abc==",
            "203.0.113.5", "Mozilla/5.0", DateTimeOffset.UtcNow);

        var act = () => document.ApplySignature(
            "Jane Client", "jane@example.com", SignatureType.Typed, "data:image/png;base64,abc==",
            "203.0.113.5", "Mozilla/5.0", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [MemberData(nameof(MutatorsRejectedOnceLocked))]
    public void Mutators_throw_once_the_document_is_locked(Action<Document> mutate)
    {
        var document = CreateDocument();
        document.AddLineItem("Research", 1m, 500m);
        document.ApplySignature(
            "Jane Client", "jane@example.com", SignatureType.Drawn, "data:image/png;base64,abc==",
            "203.0.113.5", "Mozilla/5.0", DateTimeOffset.UtcNow);

        var act = () => mutate(document);

        act.Should().Throw<InvalidOperationException>();
    }

    public static IEnumerable<object[]> MutatorsRejectedOnceLocked()
    {
        yield return [new Action<Document>(d => d.AddLineItem("More work", 1m, 100m))];
        yield return [new Action<Document>(d => d.UpdateLineItem(d.LineItems.First().Id, "Renamed", 1m, 100m))];
        yield return [new Action<Document>(d => d.RemoveLineItem(d.LineItems.First().Id))];
        yield return [new Action<Document>(d => d.ChangeStatus(DocumentStatus.Sent))];
        yield return [new Action<Document>(d => d.Reschedule(DueDate.AddDays(1)))];
        yield return [new Action<Document>(d => d.ChangeTemplate(Guid.NewGuid()))];
        yield return [new Action<Document>(d => d.ChangeCurrency("EUR", "DE"))];
        yield return [new Action<Document>(d => d.ReplaceLineItems([new NewLineItem("X", 1m, 1m, null)]))];
    }

    [Fact]
    public void Dispatch_records_a_Dispatched_activity()
    {
        var document = CreateDocument();
        document.AddLineItem("Research", 1m, 500m);
        var dispatchedAt = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);

        document.Dispatch(dispatchedAt);

        document.Activities.Should().ContainSingle(a => a.Type == DocumentActivityType.Dispatched && a.OccurredAtUtc == dispatchedAt);
    }

    [Fact]
    public void RecordView_always_appends_a_Viewed_activity_but_only_reports_the_first_as_new()
    {
        var document = CreateDocument();
        var firstViewedAt = new DateTime(2026, 8, 26, 9, 0, 0, DateTimeKind.Utc);
        var secondViewedAt = firstViewedAt.AddHours(2);

        var isFirstView = document.RecordView(firstViewedAt);
        var isSecondViewFirst = document.RecordView(secondViewedAt);

        isFirstView.Should().BeTrue();
        isSecondViewFirst.Should().BeFalse();
        document.Activities.Where(a => a.Type == DocumentActivityType.Viewed).Should().HaveCount(2);
        document.FirstViewedAtUtc.Should().Be(firstViewedAt);
    }

    [Fact]
    public void RequestRevision_records_a_RevisionRequested_activity_carrying_the_feedback()
    {
        var document = CreateDocument();
        document.ChangeStatus(DocumentStatus.Sent);
        var requestedAt = new DateTime(2026, 8, 27, 8, 0, 0, DateTimeKind.Utc);

        document.RequestRevision("Please use a different color scheme.", requestedAt);

        document.Activities.Should().ContainSingle(a =>
            a.Type == DocumentActivityType.RevisionRequested &&
            a.Detail == "Please use a different color scheme." &&
            a.OccurredAtUtc == requestedAt);
    }

    [Fact]
    public void ApplySignature_records_a_Signed_activity_carrying_the_signer_name()
    {
        var document = CreateDocument();
        document.AddLineItem("Research", 1m, 500m);
        var signedAt = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

        document.ApplySignature(
            "Jane Client", "jane@example.com", SignatureType.Drawn, "data:image/png;base64,abc==",
            "203.0.113.5", "Mozilla/5.0", signedAt);

        document.Activities.Should().ContainSingle(a =>
            a.Type == DocumentActivityType.Signed && a.Detail == "Jane Client" && a.OccurredAtUtc == signedAt.UtcDateTime);
    }
}
