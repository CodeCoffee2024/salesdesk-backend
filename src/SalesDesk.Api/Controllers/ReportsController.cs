using MediatR;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Reports;

namespace SalesDesk.Api.Controllers;

/// <summary>TASK-043: the Reports page's three data sources, all workspace-scoped (via ICurrentUserService inside each handler) and sharing the same from/to date-range shape.</summary>
[ApiController]
[Route("api/reports")]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueReportDto>> GetRevenue([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRevenueReportQuery(from, to), cancellationToken);
        return Ok(result);
    }

    [HttpGet("top-customers")]
    public async Task<ActionResult<List<TopCustomerDto>>> GetTopCustomers([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTopCustomersQuery(from, to), cancellationToken);
        return Ok(result);
    }

    [HttpGet("top-products")]
    public async Task<ActionResult<List<TopProductDto>>> GetTopProducts([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTopProductsQuery(from, to), cancellationToken);
        return Ok(result);
    }
}
