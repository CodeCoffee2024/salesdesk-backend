namespace SalesDesk.Application.Common.Exceptions;

/// <summary>
/// Thrown when the caller is correctly identified but not permitted to proceed for
/// a reason beyond ordinary role authorization — e.g. their workspace has been
/// suspended by a SystemAdmin. The API layer maps this to a 403 response.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
