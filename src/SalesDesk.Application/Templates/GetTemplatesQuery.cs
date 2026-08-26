using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Templates;

public sealed record GetTemplatesQuery : IRequest<List<TemplateDto>>;

public sealed class GetTemplatesQueryHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<GetTemplatesQuery, List<TemplateDto>>
{
    public async Task<List<TemplateDto>> Handle(GetTemplatesQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var templates = await context.Templates
            .Where(t => t.WorkspaceId == workspaceId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return mapper.Map<List<TemplateDto>>(templates);
    }
}
