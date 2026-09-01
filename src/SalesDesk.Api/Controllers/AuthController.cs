using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDesk.Application.Auth;

namespace SalesDesk.Api.Controllers;

public sealed record RegisterRequest(string Email, string Password, string FullName, string WorkspaceName);

public sealed record LoginRequest(string Email, string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(request.Email, request.Password, request.FullName, request.WorkspaceName);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken);

        // Always 200, regardless of whether the address is registered — see
        // ForgotPasswordCommandHandler.
        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult<AuthResponseDto>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponseDto>> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var command = new VerifyEmailCommand(request.Token);
        var result = await sender.Send(command, cancellationToken);

        return Ok(result);
    }

    // [AllowAnonymous] because this backs two callers: the login page's "request a
    // new verification link" (never authenticated) and the in-app banner's "Resend
    // Email" button (already authenticated, but blocked by EmailVerificationBehavior
    // from every other mutation) — see ResendVerificationEmailCommand.
    [AllowAnonymous]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ResendVerificationEmailCommand(request.Email), cancellationToken);

        // Always 200, regardless of whether the address is registered or already
        // verified — see ResendVerificationEmailCommandHandler.
        return Ok();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCurrentUserQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("onboarding/complete")]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken cancellationToken)
    {
        await sender.Send(new CompleteOnboardingCommand(), cancellationToken);
        return Ok();
    }
}
