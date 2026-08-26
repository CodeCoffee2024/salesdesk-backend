using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Workspaces;

/// <summary>
/// The business/studio a SalesDesk account represents. Documents are issued "from"
/// this profile — name, tagline, address, email and logo appear on every quote and
/// invoice.
/// </summary>
public sealed class Workspace : Entity
{
    public string Name { get; private set; }

    public string? Tagline { get; private set; }

    public string? Address { get; private set; }

    public string Email { get; private set; }

    public string? LogoUrl { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Maximum documents this workspace may issue. Null means unlimited.</summary>
    public int? DocumentQuota { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Workspace()
    {
        Name = string.Empty;
        Email = string.Empty;
    }

    public Workspace(string name, string email, string? tagline = null, string? address = null, string? logoUrl = null, int? documentQuota = 100)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Tagline = tagline;
        Address = address;
        LogoUrl = logoUrl;
        IsActive = true;
        DocumentQuota = documentQuota;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string name, string email, string? tagline, string? address, string? logoUrl)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Tagline = tagline;
        Address = address;
        LogoUrl = logoUrl;
    }

    /// <summary>Blocks every user of this workspace from signing in — see LoginCommandHandler.</summary>
    public void Suspend() => IsActive = false;

    public void Activate() => IsActive = true;

    public void SetDocumentQuota(int? documentQuota)
    {
        if (documentQuota is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentQuota), documentQuota, "Document quota cannot be negative.");
        }

        DocumentQuota = documentQuota;
    }
}
