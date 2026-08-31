using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Workspaces;

/// <summary>Backs PUT /api/workspace/profile (TASK-029 onboarding step 1) — lets a workspace admin set the business name/logo/contact info shown on every issued document.</summary>
public sealed record UpdateWorkspaceProfileCommand(string Name, string Email, string? Tagline, string? Address, string? LogoUrl) : IRequest<WorkspaceProfileDto>;

public sealed class UpdateWorkspaceProfileCommandValidator : AbstractValidator<UpdateWorkspaceProfileCommand>
{
    public UpdateWorkspaceProfileCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.LogoUrl).Must(BeAValidUrl).WithMessage("Logo URL must be a valid http(s) URL.").When(c => !string.IsNullOrWhiteSpace(c.LogoUrl));
    }

    private static bool BeAValidUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
}

public sealed class UpdateWorkspaceProfileCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<UpdateWorkspaceProfileCommand, WorkspaceProfileDto>
{
    public async Task<WorkspaceProfileDto> Handle(UpdateWorkspaceProfileCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var workspace = await context.Workspaces.SingleAsync(w => w.Id == workspaceId, cancellationToken);

        workspace.UpdateProfile(request.Name, request.Email, request.Tagline, request.Address, request.LogoUrl);
        await context.SaveChangesAsync(cancellationToken);

        return new WorkspaceProfileDto
        {
            Name = workspace.Name,
            Email = workspace.Email,
            Tagline = workspace.Tagline,
            Address = workspace.Address,
            LogoUrl = workspace.LogoUrl
        };
    }
}
