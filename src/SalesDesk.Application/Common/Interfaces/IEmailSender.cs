namespace SalesDesk.Application.Common.Interfaces;

/// <summary>ReplyTo (TASK-034): set to a workspace's own business email on customer-facing sends, so a reply reaches the workspace rather than the platform's shared sending address — the From address itself stays the single authenticated system sender regardless (Resend:FromAddress), since per-workspace sending domains aren't supported.</summary>
public sealed record EmailMessage(string To, string? Cc, string Subject, string HtmlBody, string? ReplyTo = null);

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
