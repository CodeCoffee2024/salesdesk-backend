using SalesDesk.Domain.Users;

namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// The identity of the caller making the current request, read from the validated
/// JWT's claims. Implemented in the API layer (the only layer that sees HttpContext).
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Role? Role { get; }

    Guid? WorkspaceId { get; }

    /// <summary>TASK-030: read off the JWT's email_verified claim (refreshed whenever VerifyEmailCommand reissues a token) so EmailVerificationBehavior can gate mutations without a database round-trip per request. False for an unauthenticated caller.</summary>
    bool IsEmailVerified { get; }
}
