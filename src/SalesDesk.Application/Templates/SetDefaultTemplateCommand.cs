using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Templates;

public sealed record SetDefaultTemplateCommand(Guid Id) : IRequest<TemplateDto>;

public sealed class SetDefaultTemplateCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<SetDefaultTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(SetDefaultTemplateCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var template = await context.Templates
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Template), request.Id);

        var currentDefaults = await context.Templates
            .Where(t => t.WorkspaceId == workspaceId && t.IsDefault && t.Id != request.Id)
            .ToListAsync(cancellationToken);

        foreach (var previousDefault in currentDefaults)
        {
            previousDefault.UnmarkAsDefault();
        }

        template.MarkAsDefault();
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<TemplateDto>(template);
    }
}
