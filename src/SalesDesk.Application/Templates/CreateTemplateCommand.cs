using AutoMapper;
using FluentValidation;
using MediatR;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Templates;

namespace SalesDesk.Application.Templates;

public sealed record CreateTemplateCommand(string Name, TemplateTargetType TargetType, string? Description, string? AccentColor, string? ContentHtml = null)
    : IRequest<TemplateDto>;

public sealed class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator() => RuleFor(t => t.Name).NotEmpty();
}

public sealed class CreateTemplateCommandHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<CreateTemplateCommand, TemplateDto>
{
    public async Task<TemplateDto> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = new Template(
            currentUser.RequireWorkspaceId(), request.Name, request.TargetType, request.Description, request.AccentColor,
            contentHtml: request.ContentHtml);

        context.Templates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<TemplateDto>(template);
    }
}
