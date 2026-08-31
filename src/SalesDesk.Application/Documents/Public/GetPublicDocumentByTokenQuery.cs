using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents.Public;

/// <summary>Backs GET /api/public/documents/{token} — the anonymous, client-facing document view (TASK-023/024).</summary>
public sealed record GetPublicDocumentByTokenQuery(Guid Token) : IRequest<PublicDocumentDto>;

public sealed class GetPublicDocumentByTokenQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicDocumentByTokenQuery, PublicDocumentDto>
{
    public async Task<PublicDocumentDto> Handle(GetPublicDocumentByTokenQuery request, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.LineItems)
            .Include(d => d.Signature)
            .FirstOrDefaultAsync(d => d.PublicToken == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Token);

        var workspace = await context.Workspaces
            .FirstAsync(w => w.Id == document.WorkspaceId, cancellationToken);

        return PublicDocumentMapper.ToDto(document, workspace.Name, workspace.LogoUrl);
    }
}
