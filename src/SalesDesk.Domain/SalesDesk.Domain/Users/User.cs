using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Users;

/// <summary>
/// A person who can sign in to SalesDesk. Every user belongs to exactly one
/// <see cref="Workspaces.Workspace"/> and holds a single <see cref="Users.Role"/>
/// within it.
/// </summary>
public sealed class User : Entity
{
    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string FullName { get; private set; }

    public Role Role { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private User()
    {
        Email = string.Empty;
        PasswordHash = string.Empty;
        FullName = string.Empty;
    }

    public User(string email, string passwordHash, string fullName, Role role, Guid workspaceId)
    {
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email)).ToLowerInvariant();
        PasswordHash = Guard.AgainstNullOrWhiteSpace(passwordHash, nameof(passwordHash));
        FullName = Guard.AgainstNullOrWhiteSpace(fullName, nameof(fullName));
        Role = role;
        WorkspaceId = Guard.AgainstEmpty(workspaceId, nameof(workspaceId));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = Guard.AgainstNullOrWhiteSpace(passwordHash, nameof(passwordHash));
    }

    public void ChangeRole(Role role)
    {
        Role = role;
    }
}
