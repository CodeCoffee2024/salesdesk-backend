namespace SalesDesk.Application.Common.Exceptions;

/// <summary>
/// Thrown by a handler when a requested entity doesn't exist. The API layer maps
/// this to a 404 response.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"\"{entityName}\" ({key}) was not found.")
    {
    }
}
