using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Templates;

public sealed record UpdateTemplateCommand(Guid Id, string Name, TemplateTargetType TargetType, string? Description, string? AccentColor)
    : IRequest<TemplateDto>;

public sealed class UpdateTemplateCommandValidator : AbstractValidator<UpdateTemplateCommand>
{
    public UpdateTemplateCommandValidator() => RuleFor(t => t.Name).NotEmpty();
}

public sealed class UpdateTemplateCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<UpdateTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(UpdateTemplateCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var template = await context.Templates
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(Template), request.Id);

        template.UpdateDetails(request.Name, request.TargetType, request.Description, request.AccentColor);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<TemplateDto>(template);
    }
}
