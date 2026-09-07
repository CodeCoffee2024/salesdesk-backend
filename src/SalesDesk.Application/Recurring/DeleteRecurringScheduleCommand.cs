using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Recurring;

public sealed record DeleteRecurringScheduleCommand(Guid Id) : IRequest;

public sealed class DeleteRecurringScheduleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser) : IRequestHandler<DeleteRecurringScheduleCommand>
{
    public async Task Handle(DeleteRecurringScheduleCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var schedule = await context.RecurringSchedules
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(RecurringSchedule), request.Id);

        // Cancelling a retainer schedule never touches documents it already
        // generated — those are independent, already-issued Documents (see
        // RecurringScheduleConfiguration's line-item cascade, which only removes
        // the schedule's own template line items, not any Document).
        context.RecurringSchedules.Remove(schedule);
        await context.SaveChangesAsync(cancellationToken);
    }
}
