using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Admin;

/// <summary>Global audit log, newest first — TASK-017 AC4.</summary>
public sealed record GetAuditLogQuery(string? Search, int Page = 1, int PageSize = 25) : IRequest<PagedResult<AuditLogEntryDto>>;

public sealed class GetAuditLogQueryHandler(IApplicationDbContext context) : IRequestHandler<GetAuditLogQuery, PagedResult<AuditLogEntryDto>>
{
    public async Task<PagedResult<AuditLogEntryDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;

        var query = context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(a => a.EventType.ToLower().Contains(term) || a.Message.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogEntryDto
            {
                Id = a.Id,
                EventType = a.EventType,
                Message = a.Message,
                WorkspaceId = a.WorkspaceId,
                UserId = a.UserId,
                OccurredAtUtc = a.OccurredAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogEntryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
