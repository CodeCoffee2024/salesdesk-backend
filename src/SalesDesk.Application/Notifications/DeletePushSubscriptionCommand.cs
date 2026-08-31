using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Notifications;

/// <summary>Backs DELETE /api/push/subscriptions (TASK-027) — the "disable notifications" path. Not scoped to the calling user: unsubscribing a browser should always work even if a different user's session created it (a shared or handed-off device).</summary>
public sealed record DeletePushSubscriptionCommand(string Endpoint) : IRequest;

public sealed class DeletePushSubscriptionCommandValidator : AbstractValidator<DeletePushSubscriptionCommand>
{
    public DeletePushSubscriptionCommandValidator()
    {
        RuleFor(c => c.Endpoint).NotEmpty();
    }
}

public sealed class DeletePushSubscriptionCommandHandler(IApplicationDbContext context) : IRequestHandler<DeletePushSubscriptionCommand>
{
    public async Task Handle(DeletePushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var existing = await context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, cancellationToken);

        if (existing is null)
        {
            return;
        }

        context.PushSubscriptions.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }
}
