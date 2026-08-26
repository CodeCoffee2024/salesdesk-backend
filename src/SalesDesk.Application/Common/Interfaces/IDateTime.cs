namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// Abstracts the system clock so handlers that stamp "today" (e.g. a new
/// document's issue date, the dashboard's "this year"/"this quarter" windows) stay
/// deterministic and testable instead of calling <see cref="DateTimeOffset.UtcNow"/>
/// directly.
/// </summary>
public interface IDateTime
{
    DateTimeOffset UtcNow { get; }
}
