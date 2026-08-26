using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents;

/// <summary>
/// Shared by every handler that mints a new document number
/// ("{QUO|INV}-{year}-{sequence}"), computed from the highest existing sequence for
/// that type/year. Not concurrency-safe under simultaneous creates — two requests
/// could compute the same number, and the second would fail on the unique index —
/// acceptable for this application's expected load, but worth knowing if that ever
/// changes.
/// </summary>
internal static class DocumentNumbering
{
    public static async Task<string> GenerateNextAsync(
        IApplicationDbContext context, Guid workspaceId, DocumentType type, DateOnly issueDate, CancellationToken cancellationToken)
    {
        var prefix = type == DocumentType.Quote ? "QUO" : "INV";
        var yearPrefix = $"{prefix}-{issueDate.Year}-";

        var existingNumbers = await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == type && d.DocumentNumber.StartsWith(yearPrefix))
            .Select(d => d.DocumentNumber)
            .ToListAsync(cancellationToken);

        var nextSequence = existingNumbers
            .Select(number => int.TryParse(number.AsSpan(yearPrefix.Length), out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{yearPrefix}{nextSequence:000}";
    }
}
