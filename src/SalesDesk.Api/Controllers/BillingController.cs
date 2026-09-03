using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Api.Authorization;
using SalesDesk.Application.Billing;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Workspaces;

namespace SalesDesk.Api.Controllers;

public sealed record CreateCheckoutSessionRequest(string Tier, string BillingCycle);

public sealed record SubmitGCashPaymentRequest(
    string Tier, string BillingCycle, string GCashReferenceNumber, string SenderName, string SenderMobileNumber, string? ScreenshotDataUrl);

public sealed record RequestSubscriptionUpgradeRequest(string Tier, string BillingCycle, string? Note);

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

    /// <summary>GET /api/workspace/billing/gcash-details — TASK-039: the platform's own GCash receiving account plus PH pricing, for the "Pay via GCash" modal.</summary>
    [HttpGet("gcash-details")]
    public async Task<ActionResult<GCashPaymentDetailsDto>> GetGCashDetails(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetGCashPaymentDetailsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/workspace/billing/gcash-submit — TASK-039: submits a proof-of-payment claim, notifies the admin, and puts the workspace into pending-verification.</summary>
    [Authorize(Policy = Policies.CanManage)]
    [HttpPost("gcash-submit")]
    public async Task<ActionResult<GCashSubmissionConfirmationDto>> SubmitGCashPayment([FromBody] SubmitGCashPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SubmitGCashPaymentCommand(request.Tier, request.BillingCycle, request.GCashReferenceNumber, request.SenderName, request.SenderMobileNumber, request.ScreenshotDataUrl),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/workspace/billing/upgrade-request — the fallback for any workspace with no configured payment method (not PH/GCash, no card gateway): asks the platform admin to activate the tier manually.</summary>
    [Authorize(Policy = Policies.CanManage)]
    [HttpPost("upgrade-request")]
    public async Task<ActionResult<UpgradeRequestConfirmationDto>> RequestUpgrade([FromBody] RequestSubscriptionUpgradeRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RequestSubscriptionUpgradeCommand(request.Tier, request.BillingCycle, request.Note), cancellationToken);
        return Ok(result);
    }
}
