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

    /// <summary>SHA-256 hex digest of the current outstanding password-reset token, if any — never the raw token itself, so a database leak alone can't be used to reset the account.</summary>
    public string? PasswordResetTokenHash { get; private set; }

    public DateTime? PasswordResetTokenExpiresAtUtc { get; private set; }

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

    /// <summary>Issuing a new token invalidates whatever's currently outstanding — only the most recently requested reset link ever works.</summary>
    public void IssuePasswordResetToken(string tokenHash, DateTime expiresAtUtc)
    {
        PasswordResetTokenHash = Guard.AgainstNullOrWhiteSpace(tokenHash, nameof(tokenHash));
        PasswordResetTokenExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>
    /// Validates <paramref name="tokenHash"/> against the outstanding reset token and,
    /// if it matches and hasn't expired, consumes it (clears the stored token so it
    /// can't be replayed) in the same call. Returns false — leaving any existing
    /// token in place — for a mismatched or expired hash, since a wrong guess
    /// shouldn't invalidate a link the user hasn't used yet.
    /// </summary>
    public bool TryConsumePasswordResetToken(string tokenHash, DateTime nowUtc)
    {
        if (PasswordResetTokenHash is null || PasswordResetTokenExpiresAtUtc is null)
        {
            return false;
        }

        if (PasswordResetTokenHash != tokenHash || PasswordResetTokenExpiresAtUtc < nowUtc)
        {
            return false;
        }

        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAtUtc = null;
        return true;
    }
}
