using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Billing;
using SalesDesk.Application.Common.Exceptions;

namespace SalesDesk.Api.Controllers;

/// <summary>
/// TASK-039: the "Approve subscription" link inside the GCash payment admin
/// notification email — a bare GET, clicked straight from an inbox, with no admin
/// login involved. The random token in the query string is the only credential
/// (see GCashPaymentSubmission.ApprovalTokenHash / SecureTokens); [AllowAnonymous]
/// here is deliberate, matching the same shape as the public document view link
/// (GetPublicDocumentByTokenQuery), not an oversight. Kept separate from the
/// SystemAdmin-login-gated controllers under Controllers/Admin/ since this one is
/// intentionally reachable without a session.
/// </summary>
[ApiController]
[Route("api/admin/subscriptions")]
public sealed class SubscriptionApprovalController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("approve")]
    public async Task<ContentResult> Approve([FromQuery] string token, CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(new ApproveGCashPaymentCommand(token), cancellationToken);
            var heading = result.WasAlreadyApproved ? "Already approved" : "Subscription approved";
            var detail = result.WasAlreadyApproved
                ? $"{result.WorkspaceName} was already upgraded to {result.Tier} (active through {result.ExpiresAtUtc:MMM d, yyyy})."
                : $"{result.WorkspaceName} is now on {result.Tier} ({result.BillingCycle}), active through {result.ExpiresAtUtc:MMM d, yyyy}. A confirmation email has been sent.";

            return Page(heading, detail, isError: false);
        }
        catch (NotFoundException)
        {
            return Page("Link not recognized", "This approval link doesn't match any pending GCash payment submission. It may have already been used with a different token, or the request no longer exists.", isError: true);
        }
    }

    private ContentResult Page(string heading, string detail, bool isError) =>
        new()
        {
            ContentType = "text/html; charset=utf-8",
            StatusCode = isError ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            Content = $"""
                <!doctype html>
                <html>
                <head><meta charset="utf-8"><title>{heading} — SalesDesk</title></head>
                <body style="font-family:-apple-system,'Segoe UI',Roboto,sans-serif;background:#f4f5f9;padding:48px 16px;">
                  <div style="max-width:480px;margin:0 auto;background:#fff;border-radius:12px;border:1px solid #e2e5ee;padding:32px;">
                    <div style="font-size:20px;font-weight:700;color:{(isError ? "#c4302b" : "#2451f5")};margin-bottom:12px;">{heading}</div>
                    <p style="color:#14192b;font-size:14px;line-height:1.6;">{System.Net.WebUtility.HtmlEncode(detail)}</p>
                  </div>
                </body>
                </html>
                """
        };
}
