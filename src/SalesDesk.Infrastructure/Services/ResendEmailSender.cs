using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Sends real email via Resend's HTTP API once Resend:ApiKey is configured — see
/// DependencyInjection, which only registers this in place of LogEmailSender when
/// that key is present. Resend:FromAddress defaults to Resend's own shared test
/// sender (only deliverable to the account's own signup address until a custom
/// domain is verified in the Resend dashboard); set it to an address on a verified
/// domain (e.g. reminders@codekopi.com) once ready for real delivery.
/// </summary>
public sealed class ResendEmailSender(HttpClient httpClient, IConfiguration configuration, ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var from = configuration["Resend:FromAddress"];
        var payload = new
        {
            from = string.IsNullOrWhiteSpace(from) ? "onboarding@resend.dev" : from,
            to = new[] { message.To },
            cc = message.Cc is null ? null : new[] { message.Cc },
            subject = message.Subject,
            html = message.HtmlBody
        };

        using var response = await httpClient.PostAsJsonAsync("emails", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Resend email send failed ({Status}): {Body}", response.StatusCode, body);
        }
    }
}
