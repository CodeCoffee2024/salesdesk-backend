using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Customers;

public sealed record GetCustomersQuery : IRequest<List<CustomerDto>>;

public sealed class GetCustomersQueryHandler(IApplicationDbContext context, IMapper mapper, ICurrentUserService currentUser)
    : IRequestHandler<GetCustomersQuery, List<CustomerDto>>
{
    public async Task<List<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();

        var customers = await context.Customers
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var lifetimeValueByCustomerId = await context.Documents
            .Where(d => d.WorkspaceId == workspaceId && d.Type == DocumentType.Invoice && d.Status == DocumentStatus.Paid)
            .GroupBy(d => d.CustomerId)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(d => d.Total) })
            .ToDictionaryAsync(g => g.CustomerId, g => g.Total, cancellationToken);

        var dtos = mapper.Map<List<CustomerDto>>(customers);
        foreach (var dto in dtos)
        {
            dto.LifetimeValue = lifetimeValueByCustomerId.GetValueOrDefault(dto.Id);
        }

        return dtos;
    }
}
