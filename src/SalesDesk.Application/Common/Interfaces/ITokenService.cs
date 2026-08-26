using SalesDesk.Domain.Users;

namespace SalesDesk.Application.Common.Interfaces;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues the bearer token a client presents on subsequent requests. Implemented in
/// Infrastructure so Application stays free of JWT-library specifics.
/// </summary>
public interface ITokenService
{
    AccessToken IssueToken(User user);

    /// <summary>
    /// Issues a token for <paramref name="target"/> on behalf of a SystemAdmin
    /// "viewing as" that user (TASK-017 admin console), with a fixed <paramref
    /// name="lifetime"/> deliberately shorter than the normal <c>Jwt:ExpiryMinutes</c>
    /// config — an impersonation session should expire quickly regardless of how the
    /// platform's ordinary token lifetime is configured.
    /// </summary>
    AccessToken IssueImpersonationToken(User target, TimeSpan lifetime);
}
