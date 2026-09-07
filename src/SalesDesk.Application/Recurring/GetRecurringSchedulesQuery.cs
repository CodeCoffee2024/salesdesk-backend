using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Recurring;

public sealed record GetRecurringSchedulesQuery : IRequest<List<RecurringScheduleDto>>;

public sealed class GetRecurringSchedulesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<GetRecurringSchedulesQuery, List<RecurringScheduleDto>>
{
    public async Task<List<RecurringScheduleDto>> Handle(GetRecurringSchedulesQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();

        var schedules = await context.RecurringSchedules
            .Include(s => s.Customer)
            .Include(s => s.Template)
            .Include(s => s.LineItems)
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.NextRunDate)
            .ToListAsync(cancellationToken);

        return schedules.Select(s => s.ToDto()).ToList();
    }
}
