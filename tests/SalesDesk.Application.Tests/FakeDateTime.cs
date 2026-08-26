using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Tests;

public sealed class FakeDateTime(DateTimeOffset utcNow) : IDateTime
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}
