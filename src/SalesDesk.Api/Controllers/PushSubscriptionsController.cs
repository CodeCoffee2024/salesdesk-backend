using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SalesDesk.Application.Notifications;

namespace SalesDesk.Api.Controllers;

public sealed record SavePushSubscriptionRequest(string Endpoint, string P256dhKey, string AuthKey);

public sealed record DeletePushSubscriptionRequest(string Endpoint);

[ApiController]
[Route("api/push")]
public sealed class PushSubscriptionsController(ISender sender, IConfiguration configuration) : ControllerBase
{
    /// <summary>The public half of the VAPID keypair, needed client-side to call pushManager.subscribe() — safe to expose to anyone, by design of the Web Push protocol.</summary>
    [AllowAnonymous]
    [HttpGet("vapid-public-key")]
    public ActionResult<string> GetVapidPublicKey() => Ok(configuration["WebPush:VapidPublicKey"] ?? string.Empty);

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe([FromBody] SavePushSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SavePushSubscriptionCommand(request.Endpoint, request.P256dhKey, request.AuthKey), cancellationToken);
        return Ok();
    }

    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Unsubscribe([FromBody] DeletePushSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePushSubscriptionCommand(request.Endpoint), cancellationToken);
        return Ok();
    }
}
