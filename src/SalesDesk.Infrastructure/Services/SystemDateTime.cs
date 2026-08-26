using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

public sealed class SystemDateTime : IDateTime
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
