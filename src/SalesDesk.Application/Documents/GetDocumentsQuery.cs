using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents;

/// <summary>
/// Backs GET /api/documents?type=all|quote|invoice&amp;status=...&amp;search=....
/// <paramref name="Type"/>/<paramref name="Status"/> are matched case-insensitively
/// against the enum names; an unrecognized or "all" type/status is treated as "no
/// filter" rather than an error, matching the reference product's tab behavior.
/// </summary>
public sealed record GetDocumentsQuery(string? Type, string? Status, string? Search) : IRequest<List<DocumentDto>>;

public sealed class GetDocumentsQueryHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<GetDocumentsQuery, List<DocumentDto>>
{
    public async Task<List<DocumentDto>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();

        var query = context.Documents
            .Include(d => d.Customer)
            .Include(d => d.Template)
            .Include(d => d.LineItems)
            .Where(d => d.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(request.Type) &&
            !string.Equals(request.Type, "all", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<DocumentType>(request.Type, ignoreCase: true, out var type))
        {
            query = query.Where(d => d.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<DocumentStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(d => d.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // ToLower()/Contains() (not EF.Functions.ILike) so this stays portable
            // across providers — Application must not assume PostgreSQL.
            var search = request.Search.Trim().ToLower();
            query = query.Where(d =>
                d.DocumentNumber.ToLower().Contains(search) ||
                d.Customer!.Name.ToLower().Contains(search) ||
                d.Customer!.Company.ToLower().Contains(search));
        }

        var documents = await query
            .OrderByDescending(d => d.IssueDate)
            .ToListAsync(cancellationToken);

        return mapper.Map<List<DocumentDto>>(documents);
    }
}
