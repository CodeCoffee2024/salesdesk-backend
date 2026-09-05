using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Admin;

/// <summary>
/// TASK-035: backs POST /api/admin/seed-demo. Provisions (or, on a repeat call,
/// wipes and re-provisions) a single fixed demo workspace, "Lumina Event Hosting
/// &amp; Production", themed for the landing page's own event-hosts ICP, with a
/// realistic customer/product/document dataset spanning past, current, and future
/// months so the dashboard and screenshots never show an empty state. Meant for
/// internal QA, marketing screenshots/recordings, and live demos, never a real
/// tenant. SystemAdmin-only (enforced by the controller's [Authorize]) and refused
/// outright in Production (enforced by the controller, which is the layer that
/// actually knows the hosting environment).
/// </summary>
public sealed record SeedDemoWorkspaceCommand : IRequest<SeedDemoWorkspaceResultDto>;

public sealed class SeedDemoWorkspaceResultDto
{
    public Guid WorkspaceId { get; init; }
    public string LoginEmail { get; init; } = string.Empty;
    /// <summary>Freshly generated on every call (including a re-seed) rather than a fixed hardcoded value, so a demo login isn't a standing, guessable credential.</summary>
    public string LoginPassword { get; init; } = string.Empty;
    public int CustomersCreated { get; init; }
    public int DocumentsCreated { get; init; }
    public bool WasReseed { get; init; }
}

public sealed class SeedDemoWorkspaceCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    : IRequestHandler<SeedDemoWorkspaceCommand, SeedDemoWorkspaceResultDto>
{
    // Fixed and well-known on purpose (not randomly generated per call): idempotency
    // means finding the SAME demo account/workspace again next time, not
    // accumulating a new one on every run. See the Idempotency Guardrail.
    private const string DemoLoginEmail = "demo@luminaeventhosting.com";

    public async Task<SeedDemoWorkspaceResultDto> Handle(SeedDemoWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == DemoLoginEmail, cancellationToken);
        var wasReseed = existingUser is not null;

        Workspace workspace;
        User user;

        if (existingUser is not null)
        {
            workspace = await context.Workspaces.FirstAsync(w => w.Id == existingUser.WorkspaceId, cancellationToken);
            user = existingUser;

            // Wipe order matters: Documents first (Customer/Template are Restrict,
            // not Cascade, on the Document FK (see DocumentConfiguration), then the
            // catalog/customer rows they referenced. Flushed immediately so the
            // fresh inserts below don't collide with not-yet-deleted unique indexes
            // (DocumentNumber, etc.) still pending in the same change tracker.
            context.Documents.RemoveRange(await context.Documents.Where(d => d.WorkspaceId == workspace.Id).ToListAsync(cancellationToken));
            await context.SaveChangesAsync(cancellationToken);
            context.Customers.RemoveRange(await context.Customers.Where(c => c.WorkspaceId == workspace.Id).ToListAsync(cancellationToken));
            context.Products.RemoveRange(await context.Products.Where(p => p.WorkspaceId == workspace.Id).ToListAsync(cancellationToken));
            context.Templates.RemoveRange(await context.Templates.Where(t => t.WorkspaceId == workspace.Id).ToListAsync(cancellationToken));
            await context.SaveChangesAsync(cancellationToken);

            workspace.UpdateProfile(
                DemoWorkspaceData.Name, DemoWorkspaceData.Email, DemoWorkspaceData.Tagline, DemoWorkspaceData.Address,
                logoUrl: null, country: "US", defaultCurrency: "USD", timeZoneId: "America/New_York");
        }
        else
        {
            workspace = new Workspace(
                DemoWorkspaceData.Name, DemoWorkspaceData.Email, DemoWorkspaceData.Tagline, DemoWorkspaceData.Address,
                country: "US", defaultCurrency: "USD");
            context.Workspaces.Add(workspace);

            user = new User(DemoLoginEmail, passwordHasher.Hash(GeneratePassword()), "Marcus Reyes", Role.WorkspaceAdmin, workspace.Id);
            context.Users.Add(user);
        }

        var password = GeneratePassword();
        user.ChangePasswordHash(passwordHasher.Hash(password));
        user.MarkEmailVerified();

        var (customers, products, templates, documents) = DemoWorkspaceData.Build(workspace.Id);

        context.Customers.AddRange(customers);
        context.Products.AddRange(products);
        context.Templates.AddRange(templates);
        context.Documents.AddRange(documents);

        await context.SaveChangesAsync(cancellationToken);

        return new SeedDemoWorkspaceResultDto
        {
            WorkspaceId = workspace.Id,
            LoginEmail = DemoLoginEmail,
            LoginPassword = password,
            CustomersCreated = customers.Count,
            DocumentsCreated = documents.Count,
            WasReseed = wasReseed
        };
    }

    private static string GeneratePassword()
    {
        // Readable-enough to relay in an admin response/screenshot, still random
        // per call: "Demo-" plus 12 base32-ish characters from cryptographic
        // randomness (RandomNumberGenerator, not Guid/Random, since this is a real
        // login credential even though the account it unlocks is a demo one).
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var chars = new char[12];
        var bytes = RandomNumberGenerator.GetBytes(chars.Length);
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }
        return $"Demo-{new string(chars)}";
    }
}
