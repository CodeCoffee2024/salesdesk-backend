using Microsoft.Extensions.Logging;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Stand-in <see cref="IEmailSender"/> for as long as no real transactional-email
/// provider is configured: logs the message instead of dispatching it, so the
/// reminder engine (TASK-025) runs end-to-end — trigger rules, suppression,
/// per-workspace settings — without an email account/subscription being a
/// prerequisite. Swap in a real provider (Postmark, Resend, SendGrid, SMTP, ...) by
/// registering a different <see cref="IEmailSender"/> implementation in
/// <see cref="DependencyInjection"/> when one is chosen; see
/// docs/research/TASK-DEPLOYMENT.md.
/// </summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email (not sent, no provider configured): To={To} Cc={Cc} ReplyTo={ReplyTo} Subject={Subject}",
            message.To, message.Cc ?? "(none)", message.ReplyTo ?? "(none)", message.Subject);

        return Task.CompletedTask;
    }
}
