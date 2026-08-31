using System.Security.Cryptography;
using System.Text;

namespace SalesDesk.Application.Auth;

/// <summary>
/// Generates and hashes password-reset tokens. The raw token goes out in the email
/// link and is never persisted; only its SHA-256 hash is stored on <c>User</c>, so a
/// database leak alone can't be replayed as a working reset link.
/// </summary>
internal static class PasswordResetTokens
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
