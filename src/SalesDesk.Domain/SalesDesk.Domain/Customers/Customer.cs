using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Customers;

/// <summary>
/// A person or company a workspace issues quotes and invoices to.
/// </summary>
public sealed class Customer : Entity
{
    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; }

    public string Company { get; private set; }

    public string Email { get; private set; }

    public string? Phone { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Customer()
    {
        Name = string.Empty;
        Company = string.Empty;
        Email = string.Empty;
    }

    public Customer(Guid workspaceId, string name, string company, string email, string? phone = null)
    {
        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Company = Guard.AgainstNullOrWhiteSpace(company, nameof(company));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Phone = phone;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(string name, string company, string email, string? phone)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Company = Guard.AgainstNullOrWhiteSpace(company, nameof(company));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        Phone = phone;
    }
}
