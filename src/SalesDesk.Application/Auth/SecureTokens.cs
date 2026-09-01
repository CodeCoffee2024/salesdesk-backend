using System.Security.Cryptography;
using System.Text;

namespace SalesDesk.Application.Auth;

/// <summary>
/// Generates and hashes single-use, time-bound tokens for links that go out over
/// email — password resets and, since TASK-030, email verification. The raw token
/// goes out in the link and is never persisted; only its SHA-256 hash is stored on
/// <c>User</c>, so a database leak alone can't be replayed as a working link.
/// </summary>
internal static class SecureTokens
{
    public static (string RawToken, string Hash) Generate()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        return (rawToken, Hash(rawToken));
    }

    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
