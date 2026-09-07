using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Recurring;

public sealed record ResumeRecurringScheduleCommand(Guid Id) : IRequest<RecurringScheduleDto>;

public sealed class ResumeRecurringScheduleCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<ResumeRecurringScheduleCommand, RecurringScheduleDto>
{
    public async Task<RecurringScheduleDto> Handle(ResumeRecurringScheduleCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();

        var schedule = await context.RecurringSchedules
            .Include(s => s.Customer)
            .Include(s => s.Template)
            .Include(s => s.LineItems)
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new NotFoundException(nameof(RecurringSchedule), request.Id);

        schedule.Resume();
        await context.SaveChangesAsync(cancellationToken);

        return schedule.ToDto();
    }
}
