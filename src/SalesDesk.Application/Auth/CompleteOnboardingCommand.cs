using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Auth;

/// <summary>Backs POST /api/auth/onboarding/complete (TASK-029) — called whether the user finished every checklist step, skipped the tour, or dismissed it outright; all three are "don't show this again."</summary>
public sealed record CompleteOnboardingCommand : IRequest;

public sealed class CompleteOnboardingCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<CompleteOnboardingCommand>
{
    public async Task Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("No user associated with the current session.");

        var user = await context.Users.SingleAsync(u => u.Id == userId && u.WorkspaceId == workspaceId, cancellationToken);
        user.CompleteOnboarding();
        await context.SaveChangesAsync(cancellationToken);
    }
}
