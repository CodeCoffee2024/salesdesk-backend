using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Infrastructure.Persistence.Seed;

/// <summary>
/// Populates a freshly-migrated database with representative demo data, mirroring
/// the shape of the reference product studied in RESEARCH-001 (one workspace, a
/// handful of customers/products/templates, and a few documents in different
/// lifecycle states) so the API has something to serve from day one.
///
/// Idempotent: bails out if the workspace already exists, so it's safe to call on
/// every startup rather than only on first run.
/// </summary>
public static class SalesDeskDbContextSeeder
{
    public static async Task SeedAsync(SalesDeskDbContext context, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        if (await context.Workspaces.AnyAsync(cancellationToken))
        {
            return;
        }

        var workspace = new Workspace(
            "Northline",
            "hello@northline.studio",
            tagline: "Creative studio",
            address: "14 Rizal Avenue, Makati, Metro Manila");

        // Dev-only login so a fresh checkout can sign in immediately:
        // admin@northline.studio / Password123!
        var workspaceAdmin = new User(
            "admin@northline.studio", passwordHasher.Hash("Password123!"), "Jordan Reyes", Role.WorkspaceAdmin, workspace.Id);

        var mayaChen = new Customer(workspace.Id, "Maya Chen", "Northstar Studio", "maya@northstar.studio");
        var andreSantos = new Customer(workspace.Id, "Andre Santos", "Santos & Co.", "andre@santosco.ph");
        var priyaNair = new Customer(workspace.Id, "Priya Nair", "Goodform Labs", "priya@goodform.io");
        var eliTurner = new Customer(workspace.Id, "Eli Turner", "Fieldwork Goods", "eli@fieldworkgoods.com");

        var brandIdentitySprint = new Product(
            workspace.Id, "Brand identity sprint", 4200m, ProductUnit.Project,
            "Strategy, visual direction, and a complete identity starter kit.", "Branding");
        var webDesignAndBuild = new Product(
            workspace.Id, "Web design & build", 6800m, ProductUnit.Project,
            "Responsive marketing site design and development.", "Web");
        var monthlyCreativeRetainer = new Product(
            workspace.Id, "Monthly creative retainer", 2400m, ProductUnit.Month,
            "Ongoing creative partnership, up to 20 hours per month.", "Retainer");
        var artDirection = new Product(
            workspace.Id, "Art direction", 950m, ProductUnit.Day,
            "Senior creative direction for campaigns and launches.", "Creative");

        var studioStandard = new Template(
            workspace.Id, "Studio Standard", TemplateTargetType.QuotesAndInvoices,
            "Warm, editorial layout for polished client work.", "#D9A441", isDefault: true);
        var modernMinimal = new Template(
            workspace.Id, "Modern Minimal", TemplateTargetType.QuotesAndInvoices,
            "Crisp, compact format for fast-moving projects.", "#2F6F6C");
        var friendlyQuote = new Template(
            workspace.Id, "Friendly Quote", TemplateTargetType.QuotesOnly,
            "A welcoming quote format with room for context.", "#8B5FBF");

        var invoice014 = new Document(
            workspace.Id, "INV-2026-014", DocumentType.Invoice, mayaChen.Id, studioStandard.Id,
            new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 26));
        invoice014.AddLineItem("Brand identity sprint", 1m, 4200m, brandIdentitySprint.Id);
        invoice014.ChangeStatus(DocumentStatus.Paid);

        var quote028 = new Document(
            workspace.Id, "QUO-2026-028", DocumentType.Quote, andreSantos.Id, friendlyQuote.Id,
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 24));
        quote028.AddLineItem("Web design & build", 1m, 6800m, webDesignAndBuild.Id);
        quote028.ChangeStatus(DocumentStatus.Sent);

        var invoice013 = new Document(
            workspace.Id, "INV-2026-013", DocumentType.Invoice, priyaNair.Id, studioStandard.Id,
            new DateOnly(2026, 7, 28), new DateOnly(2026, 8, 11));
        invoice013.AddLineItem("Monthly creative retainer", 1m, 2400m, monthlyCreativeRetainer.Id);
        invoice013.ChangeStatus(DocumentStatus.Overdue);

        var quote027 = new Document(
            workspace.Id, "QUO-2026-027", DocumentType.Quote, eliTurner.Id, modernMinimal.Id,
            new DateOnly(2026, 7, 24), new DateOnly(2026, 8, 7));
        quote027.AddLineItem("Art direction", 3m, 950m, artDirection.Id);
        quote027.ChangeStatus(DocumentStatus.Accepted);

        var invoice012 = new Document(
            workspace.Id, "INV-2026-012", DocumentType.Invoice, mayaChen.Id, studioStandard.Id,
            new DateOnly(2026, 7, 18), new DateOnly(2026, 8, 1));
        invoice012.AddLineItem("Brand identity sprint", 1m, 2400m, brandIdentitySprint.Id);
        invoice012.ChangeStatus(DocumentStatus.Sent);

        // "SalesDesk HQ": the platform operator's own workspace, not a real tenant —
        // exists solely to host the SystemAdmin account that opens the System Admin
        // Console (TASK-017). Unlimited quota since it issues no customer documents.
        //
        // Dev-only login: superadmin@salesdesk.app / Password123!
        var platformWorkspace = new Workspace("SalesDesk HQ", "ops@salesdesk.app", tagline: "Platform operations", documentQuota: null);
        var systemAdmin = new User(
            "superadmin@salesdesk.app", passwordHasher.Hash("Password123!"), "Sam Rivera", Role.SystemAdmin, platformWorkspace.Id);

        // Two more lightweight demo tenants so the admin console's workspace
        // directory has more than one row to search/inspect: one sitting right at
        // its document quota, one suspended — giving both non-trivial admin states
        // something real to display without a full seeded business dataset.
        //
        // Dev-only logins: admin@fieldworkcollective.com / Password123!
        //                  admin@driftwoodstudio.com / Password123!
        var atQuotaWorkspace = new Workspace("Fieldwork Collective", "hello@fieldworkcollective.com", documentQuota: 5);
        var atQuotaAdmin = new User(
            "admin@fieldworkcollective.com", passwordHasher.Hash("Password123!"), "Casey Okafor", Role.WorkspaceAdmin, atQuotaWorkspace.Id);
        var atQuotaCustomer = new Customer(atQuotaWorkspace.Id, "Nia Osei", "Harborlight Co.", "nia@harborlight.co");
        var atQuotaTemplate = new Template(atQuotaWorkspace.Id, "Collective Standard", isDefault: true);
        var atQuotaDocuments = Enumerable.Range(1, 5)
            .Select(i =>
            {
                var document = new Document(
                    atQuotaWorkspace.Id, $"QUO-2026-{100 + i}", DocumentType.Quote, atQuotaCustomer.Id, atQuotaTemplate.Id,
                    new DateOnly(2026, 8, i), new DateOnly(2026, 8, i + 14));
                document.AddLineItem("Project work", 1m, 1000m);
                return document;
            })
            .ToList();

        var suspendedWorkspace = new Workspace("Driftwood Studio", "hello@driftwoodstudio.com");
        suspendedWorkspace.Suspend();
        var suspendedAdmin = new User(
            "admin@driftwoodstudio.com", passwordHasher.Hash("Password123!"), "Robin Ashworth", Role.WorkspaceAdmin, suspendedWorkspace.Id);

        context.Workspaces.Add(workspace);
        context.Users.Add(workspaceAdmin);
        context.Customers.AddRange(mayaChen, andreSantos, priyaNair, eliTurner);
        context.Products.AddRange(brandIdentitySprint, webDesignAndBuild, monthlyCreativeRetainer, artDirection);
        context.Templates.AddRange(studioStandard, modernMinimal, friendlyQuote);
        context.Documents.AddRange(invoice014, quote028, invoice013, quote027, invoice012);

        context.Workspaces.AddRange(platformWorkspace, atQuotaWorkspace, suspendedWorkspace);
        context.Users.AddRange(systemAdmin, atQuotaAdmin, suspendedAdmin);
        context.Customers.Add(atQuotaCustomer);
        context.Templates.Add(atQuotaTemplate);
        context.Documents.AddRange(atQuotaDocuments);

        await context.SaveChangesAsync(cancellationToken);
    }
}
