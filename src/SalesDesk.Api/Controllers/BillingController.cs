using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Billing;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Workspaces;

namespace SalesDesk.Api.Controllers;

public sealed record CreateCheckoutSessionRequest(string Tier, string BillingCycle);

/// <summary>TASK-031/TASK-038: the current workspace's subscription tier, usage, and the regional pricing catalog for /settings/billing, plus the (currently stubbed — see UnconfiguredPaymentGatewayService) upgrade checkout flow.</summary>
[ApiController]
[Route("api/workspace/billing")]
public sealed class BillingController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WorkspaceBillingDto>> Get(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWorkspaceBillingQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/workspace/billing/pricing — the priced tier catalog for this workspace's own region (PH vs Global).</summary>
    [HttpGet("pricing")]
    public async Task<ActionResult<PricingCatalogDto>> GetPricing(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPricingCatalogQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/workspace/billing/checkout-session — starts an upgrade to Pro/Studio. Returns 503 until a real payment provider is configured.</summary>
    [Authorize(Policy = Policies.CanManage)]
    [HttpPost("checkout-session")]
    public async Task<ActionResult<CheckoutSession>> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateCheckoutSessionCommand(request.Tier, request.BillingCycle), cancellationToken);
        return Ok(result);
    }
}
