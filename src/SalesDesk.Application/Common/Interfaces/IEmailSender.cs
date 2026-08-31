namespace SalesDesk.Application.Common.Interfaces;

public sealed record EmailMessage(string To, string? Cc, string Subject, string HtmlBody);

/// <summary>
/// Sends a single email. Implemented in Infrastructure; until a real transactional
/// provider (Postmark, Resend, SendGrid, SMTP, ...) is configured, the registered
/// implementation logs the message instead of dispatching it, so the reminder engine
/// (TASK-025) is fully exercisable — trigger rules, suppression, settings — without
/// requiring an email account to exist yet. See docs/research/TASK-DEPLOYMENT.md.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
