using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Admin;

/// <summary>
/// TASK-035: the actual dataset SeedDemoWorkspaceCommandHandler provisions. Kept
/// separate from the handler (which owns the wipe/create lifecycle) so the content
/// itself, "what Lumina Event Hosting's business looks like", can change without
/// touching the idempotency logic. Every name, address, and line item here is
/// deliberately specific and invented, not a "Test Customer 1" / "Lorem Ipsum"
/// placeholder, per the task's marketing-asset-readiness requirement.
/// </summary>
internal static class DemoWorkspaceData
{
    public const string Name = "Lumina Event Hosting & Production";
    public const string Email = "hello@luminaeventhosting.com";
    public const string Tagline = "Emcee hosting, sound, and full-service event production";
    public const string Address = "228 Harbor View Drive, Austin, TX 78701";

    // A minimal valid 1x1 PNG, standing in for a drawn signature stroke on the one
    // seeded document that simulates an e-signed acceptance.
    private const string PlaceholderSignaturePng =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public static (List<Customer> Customers, List<Product> Products, List<Template> Templates, List<Document> Documents) Build(Guid workspaceId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var customers = BuildCustomers(workspaceId);
        var products = BuildProducts(workspaceId);
        var templates = BuildTemplates(workspaceId);
        var documents = BuildDocuments(workspaceId, today, customers, products, templates);

        return (customers, products, templates, documents);
    }

    private static List<Customer> BuildCustomers(Guid workspaceId) =>
    [
        new Customer(workspaceId, "Sarah Jenkins", "Meridian Tech Corp", "sarah.jenkins@meridiantech.com", "512-555-0148", "US"),
        new Customer(workspaceId, "Amanda Cole", "Cole & Co. Weddings", "amanda@coleandcoweddings.com", "512-555-0172", "US"),
        new Customer(workspaceId, "David Whitfield", "Whitfield Family", "dwhitfield@gmail.com", "737-555-0110", "US"),
        new Customer(workspaceId, "Priya Sharma", "Sharma Events Co.", "priya@sharmaevents.com", "512-555-0193", "US"),
        new Customer(workspaceId, "Ben Ortiz", "Ortiz-Reyes Wedding", "ben.ortiz.wedding@gmail.com", "737-555-0165", "US"),
        new Customer(workspaceId, "Rachel Kim", "Northgate Financial", "rachel.kim@northgatefinancial.com", "512-555-0121", "US"),
        new Customer(workspaceId, "Marcus Bell", "Bell Foundation", "marcus@bellfoundation.org", "512-555-0187", "US")
    ];

    private static List<Product> BuildProducts(Guid workspaceId) =>
    [
        new Product(workspaceId, "4-Hour Emcee & Hosting Services", 1200m, ProductUnit.Project,
            "Professional emcee and event hosting, up to 4 hours on-site.", "Hosting"),
        new Product(workspaceId, "Wireless Mic & Audio Setup", 400m, ProductUnit.Project,
            "Wireless microphone system, PA setup, and pre-event sound check.", "Audio"),
        new Product(workspaceId, "Overtime Rate", 150m, ProductUnit.Hour,
            "Additional hosting time beyond the contracted package.", "Hosting"),
        new Product(workspaceId, "Full-Day Wedding Coordination Package", 2200m, ProductUnit.Project,
            "Day-of coordination, vendor management, and ceremony-to-reception hosting.", "Weddings"),
        new Product(workspaceId, "DJ & Lighting Package", 950m, ProductUnit.Project,
            "DJ services with uplighting and dance-floor lighting rig.", "Production"),
        new Product(workspaceId, "Event Planning Consultation", 175m, ProductUnit.Hour,
            "Pre-event planning session covering run-of-show and logistics.", "Planning")
    ];

    private static List<Template> BuildTemplates(Guid workspaceId) =>
    [
        new Template(workspaceId, "Lumina Classic", TemplateTargetType.QuotesAndInvoices,
            "Warm, editorial layout for weddings and private events.", "#2451F5", isDefault: true,
            contentHtml: "<h2>Thank you, {{Customer.Name}}</h2>"
                + "<p>Prepared for <strong>{{Customer.Company}}</strong>, document {{Document.Number}}, due {{Document.DueDate}}.</p>"
                + "<p>We're looking forward to making your event unforgettable. Reach us any time at {{Customer.Email}}.</p>"),
        new Template(workspaceId, "Lumina Corporate", TemplateTargetType.QuotesAndInvoices,
            "Crisp, compact format for corporate and nonprofit bookings.", "#FF6A45")
    ];

    private static List<Document> BuildDocuments(
        Guid workspaceId, DateOnly today, List<Customer> customers, List<Product> products, List<Template> templates)
    {
        var classic = templates[0];
        var corporate = templates[1];

        var emcee = products[0];
        var audio = products[1];
        var overtime = products[2];
        var coordination = products[3];
        var djLighting = products[4];
        var consultation = products[5];

        var sarah = customers[0];
        var amanda = customers[1];
        var david = customers[2];
        var priya = customers[3];
        var ben = customers[4];
        var rachel = customers[5];
        var marcus = customers[6];

        var documents = new List<Document>();
        var counter = 1;

        Document NewDoc(DocumentType type, Customer customer, Template template, DateOnly issueDate, int dueDays, string currency = "USD") =>
            new(workspaceId, $"{(type == DocumentType.Quote ? "QUO" : "INV")}-{issueDate.Year}-{counter++:D3}",
                type, customer.Id, template.Id, issueDate, issueDate.AddDays(dueDays), currency, "US");

        // --- Past, Paid: deposits and balances already collected (dashboard revenue history) ---
        var paid1 = NewDoc(DocumentType.Invoice, amanda, classic, today.AddMonths(-5), 14);
        paid1.AddLineItem("Full-Day Wedding Coordination Package", 1, coordination.Price, coordination.Id);
        paid1.AddLineItem("DJ & Lighting Package", 1, djLighting.Price, djLighting.Id);
        paid1.ChangeStatus(DocumentStatus.Sent);
        paid1.ChangeStatus(DocumentStatus.Paid);
        documents.Add(paid1);

        var paid2 = NewDoc(DocumentType.Invoice, rachel, corporate, today.AddMonths(-4), 21);
        paid2.AddLineItem("4-Hour Emcee & Hosting Services", 1, emcee.Price, emcee.Id);
        paid2.AddLineItem("Wireless Mic & Audio Setup", 1, audio.Price, audio.Id);
        paid2.ChangeStatus(DocumentStatus.Sent);
        paid2.ChangeStatus(DocumentStatus.Paid);
        documents.Add(paid2);

        var paid3 = NewDoc(DocumentType.Invoice, marcus, corporate, today.AddMonths(-4), 14);
        paid3.AddLineItem("4-Hour Emcee & Hosting Services", 1, emcee.Price, emcee.Id);
        paid3.AddLineItem("Overtime Rate", 2, overtime.Price, overtime.Id);
        paid3.ChangeStatus(DocumentStatus.Sent);
        paid3.ChangeStatus(DocumentStatus.Paid);
        documents.Add(paid3);

        var paid4 = NewDoc(DocumentType.Invoice, david, classic, today.AddMonths(-3), 14);
        paid4.AddLineItem("Full-Day Wedding Coordination Package", 1, coordination.Price, coordination.Id);
        paid4.ChangeStatus(DocumentStatus.Sent);
        paid4.ChangeStatus(DocumentStatus.Paid);
        documents.Add(paid4);

        var paid5 = NewDoc(DocumentType.Invoice, priya, corporate, today.AddMonths(-2), 21);
        paid5.AddLineItem("Event Planning Consultation", 4, consultation.Price, consultation.Id);
        paid5.AddLineItem("DJ & Lighting Package", 1, djLighting.Price, djLighting.Id);
        paid5.ChangeStatus(DocumentStatus.Sent);
        paid5.ChangeStatus(DocumentStatus.Paid);
        documents.Add(paid5);

        var paid6 = NewDoc(DocumentType.Invoice, sarah, corporate, today.AddMonths(-1).AddDays(-10), 14);
        paid6.AddLineItem("4-Hour Emcee & Hosting Services", 1, emcee.Price, emcee.Id);
        paid6.ChangeStatus(DocumentStatus.Sent);
        paid6.ChangeStatus(DocumentStatus.Paid);
        documents.Add(paid6);

        // --- Recent, Overdue: an unpaid balance past its due date ---
        var overdue1 = NewDoc(DocumentType.Invoice, ben, classic, today.AddDays(-25), 14);
        overdue1.AddLineItem("Full-Day Wedding Coordination Package", 1, coordination.Price, coordination.Id);
        overdue1.AddLineItem("Overtime Rate", 3, overtime.Price, overtime.Id);
        overdue1.ChangeStatus(DocumentStatus.Sent);
        overdue1.ChangeStatus(DocumentStatus.Overdue);
        documents.Add(overdue1);

        // --- Current pipeline: sent, awaiting a decision ---
        var pending1 = NewDoc(DocumentType.Quote, priya, corporate, today.AddDays(-6), 14);
        pending1.AddLineItem("4-Hour Emcee & Hosting Services", 1, emcee.Price, emcee.Id);
        pending1.AddLineItem("Wireless Mic & Audio Setup", 1, audio.Price, audio.Id);
        pending1.ChangeStatus(DocumentStatus.Sent);
        documents.Add(pending1);

        var pending2 = NewDoc(DocumentType.Quote, david, classic, today.AddDays(-3), 14);
        pending2.AddLineItem("Full-Day Wedding Coordination Package", 1, coordination.Price, coordination.Id);
        pending2.ChangeStatus(DocumentStatus.Sent);
        documents.Add(pending2);

        // --- A client asked for changes instead of accepting ---
        var revision1 = NewDoc(DocumentType.Quote, marcus, corporate, today.AddDays(-9), 14);
        revision1.AddLineItem("DJ & Lighting Package", 1, djLighting.Price, djLighting.Id);
        revision1.AddLineItem("Overtime Rate", 2, overtime.Price, overtime.Id);
        revision1.ChangeStatus(DocumentStatus.Sent);
        revision1.RequestRevision("Could we swap the DJ package for just the lighting rig? Budget's a bit tighter than expected.", DateTime.UtcNow.AddDays(-2));
        documents.Add(revision1);

        // --- The three named scenarios from the task brief ---
        var draftOct = NewDoc(DocumentType.Quote, rachel, corporate, today.AddMonths(1), 14);
        draftOct.AddLineItem("Annual Tech Summit Emcee Package", 1, emcee.Price, emcee.Id);
        draftOct.AddLineItem("Wireless Mic & Audio Setup", 1, audio.Price, audio.Id);
        documents.Add(draftOct); // stays Draft

        var sentDec = NewDoc(DocumentType.Quote, sarah, corporate, today.AddMonths(3).AddDays(-4), 14);
        sentDec.AddLineItem("Corporate Year-End Party Hosting & Sound", 1, emcee.Price, emcee.Id);
        sentDec.AddLineItem("Wireless Mic & Audio Setup", 1, audio.Price, audio.Id);
        sentDec.AddLineItem("Overtime Rate", 2, overtime.Price, overtime.Id);
        sentDec.ChangeStatus(DocumentStatus.Sent);
        documents.Add(sentDec);

        var acceptedNov = NewDoc(DocumentType.Quote, amanda, classic, today.AddMonths(2).AddDays(-8), 14);
        acceptedNov.AddLineItem("Deluxe Wedding Host & Coordination Package", 1, coordination.Price, coordination.Id);
        acceptedNov.AddLineItem("DJ & Lighting Package", 1, djLighting.Price, djLighting.Id);
        acceptedNov.ChangeStatus(DocumentStatus.Sent);
        acceptedNov.ApplySignature("Amanda Cole", amanda.Email, SignatureType.Drawn, PlaceholderSignaturePng, "203.0.113.42", "Mozilla/5.0 (demo seed)", DateTime.UtcNow.AddDays(-1));
        documents.Add(acceptedNov);

        // --- More future pipeline, spread across the next couple of months for chart depth ---
        var future1 = NewDoc(DocumentType.Quote, ben, classic, today.AddMonths(1).AddDays(10), 14);
        future1.AddLineItem("Full-Day Wedding Coordination Package", 1, coordination.Price, coordination.Id);
        documents.Add(future1); // Draft

        var future2 = NewDoc(DocumentType.Quote, david, classic, today.AddMonths(2).AddDays(4), 14);
        future2.AddLineItem("4-Hour Emcee & Hosting Services", 1, emcee.Price, emcee.Id);
        future2.AddLineItem("DJ & Lighting Package", 1, djLighting.Price, djLighting.Id);
        future2.ChangeStatus(DocumentStatus.Sent);
        documents.Add(future2);

        var future3 = NewDoc(DocumentType.Invoice, priya, corporate, today.AddMonths(-1), 30);
        future3.AddLineItem("Event Planning Consultation", 6, consultation.Price, consultation.Id);
        future3.ChangeStatus(DocumentStatus.Sent);
        documents.Add(future3); // still open, due date in the future relative to its issue

        var future4 = NewDoc(DocumentType.Quote, marcus, corporate, today.AddMonths(3).AddDays(5), 14);
        future4.AddLineItem("4-Hour Emcee & Hosting Services", 2, emcee.Price, emcee.Id);
        documents.Add(future4); // Draft

        return documents;
    }
}
