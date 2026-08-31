using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Users;

namespace SalesDesk.Application.Notifications;

/// <summary>Backs POST /api/push/subscriptions (TASK-027) — registers or refreshes the calling user's browser subscription.</summary>
public sealed record SavePushSubscriptionCommand(string Endpoint, string P256dhKey, string AuthKey) : IRequest;

public sealed class SavePushSubscriptionCommandValidator : AbstractValidator<SavePushSubscriptionCommand>
{
    public SavePushSubscriptionCommandValidator()
    {
        RuleFor(c => c.Endpoint).NotEmpty();
        RuleFor(c => c.P256dhKey).NotEmpty();
        RuleFor(c => c.AuthKey).NotEmpty();
    }
}

public sealed class SavePushSubscriptionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<SavePushSubscriptionCommand>
{
    public async Task Handle(SavePushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("No user associated with the current session.");

        var existing = await context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, cancellationToken);

        if (existing is not null)
        {
            // Same browser re-subscribing (e.g. a different user signed into the
            // same device, or the browser rotated the keys) — replace in place
            // rather than duplicate, since Endpoint is unique.
            context.PushSubscriptions.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        context.PushSubscriptions.Add(new PushSubscription(userId, request.Endpoint, request.P256dhKey, request.AuthKey));
        await context.SaveChangesAsync(cancellationToken);
    }
}
