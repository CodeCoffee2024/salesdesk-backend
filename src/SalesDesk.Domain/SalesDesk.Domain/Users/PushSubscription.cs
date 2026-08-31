using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Users;

/// <summary>
/// A browser's Web Push subscription for one user (TASK-027) — the three fields
/// the Web Push protocol (RFC 8291/8292) needs to encrypt and address a push
/// message to that specific browser/device. <see cref="Endpoint"/> is unique
/// per browser installation, so re-subscribing the same device upserts this row
/// rather than accumulating duplicates.
/// </summary>
public sealed class PushSubscription : Entity
{
    public Guid UserId { get; private set; }

    public string Endpoint { get; private set; }

    public string P256dhKey { get; private set; }

    public string AuthKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private PushSubscription()
    {
        Endpoint = string.Empty;
        P256dhKey = string.Empty;
        AuthKey = string.Empty;
    }

    public PushSubscription(Guid userId, string endpoint, string p256dhKey, string authKey)
    {
        UserId = Guard.AgainstEmpty(userId, nameof(userId));
        Endpoint = Guard.AgainstNullOrWhiteSpace(endpoint, nameof(endpoint));
        P256dhKey = Guard.AgainstNullOrWhiteSpace(p256dhKey, nameof(p256dhKey));
        AuthKey = Guard.AgainstNullOrWhiteSpace(authKey, nameof(authKey));
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
