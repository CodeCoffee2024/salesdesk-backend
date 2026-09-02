using System.Net;

namespace SalesDesk.Application.Common.Email;

/// <summary>
/// TASK-034: wraps a plain HTML fragment in a responsive-safe (table-free, but
/// inline-styled, since most email clients strip &lt;style&gt; blocks) branded shell.
/// Two wrappers, matching the task's "Branding Leak Guardrail": <see cref="Workspace"/>
/// for anything a workspace's own customer sees (quotes, invoices, status updates),
/// <see cref="System"/> for platform/account emails (password reset, email
/// verification) that must never carry a workspace's own branding. Callers choose
/// the wrapper; this class doesn't infer which is appropriate.
/// </summary>
public static class EmailBranding
{
    // Matches the app's own Cobalt/ink design tokens (salesdesk-frontend/src/styles.scss)
    // so a system email looks like it came from the same product as the app itself.
    private const string BrandColor = "#2451f5";
    private const string InkColor = "#14192b";
    private const string MutedColor = "#5b6178";
    private const string BorderColor = "#e2e5ee";
    private const string GroundColor = "#f4f5f9";

    /// <summary>For a customer-facing email: quote/invoice notifications, activity updates. Shows the workspace's own logo (or its name as a text header when no logo is set), never the system's.</summary>
    public static string Workspace(string workspaceName, string? logoUrl, string? tagline, string? address, string workspaceEmail, string bodyHtml)
    {
        var header = string.IsNullOrWhiteSpace(logoUrl)
            ? $"<div style=\"font-size:20px;font-weight:700;color:{InkColor};\">{Encode(workspaceName)}</div>"
            : $"<img src=\"{Encode(logoUrl)}\" alt=\"{Encode(workspaceName)}\" style=\"max-height:40px;max-width:220px;display:block;\" />";

        var footerLines = new List<string> { Encode(workspaceName) };
        if (!string.IsNullOrWhiteSpace(tagline))
        {
            footerLines.Add(Encode(tagline));
        }
        if (!string.IsNullOrWhiteSpace(address))
        {
            footerLines.Add(Encode(address));
        }
        footerLines.Add(Encode(workspaceEmail));

        return Shell(header, bodyHtml, string.Join("<br/>", footerLines));
    }

    /// <summary>For a platform/account email: password reset, email verification, security alerts. Fixed system branding regardless of any workspace involved.</summary>
    public static string System(string bodyHtml)
    {
        var header = $"<div style=\"font-size:20px;font-weight:700;color:{BrandColor};\">SalesDesk</div>";
        return Shell(header, bodyHtml, "This is an account notification from SalesDesk.");
    }

    /// <summary>A prominent CTA button: every core template (TASK-034) leads with one of these to the relevant page rather than a bare link.</summary>
    public static string CtaButton(string label, string url) =>
        $"""
        <div style="text-align:center;margin:24px 0;">
          <a href="{Encode(url)}" style="display:inline-block;background:{BrandColor};color:#ffffff;text-decoration:none;font-weight:600;font-size:14px;padding:12px 28px;border-radius:8px;">{Encode(label)}</a>
        </div>
        """;

    private static string Shell(string headerHtml, string bodyHtml, string footerHtml) =>
        $"""
        <div style="background:{GroundColor};padding:32px 16px;font-family:-apple-system,'Segoe UI',Roboto,sans-serif;">
          <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid {BorderColor};">
            <div style="padding:24px 28px;border-bottom:1px solid {BorderColor};">{headerHtml}</div>
            <div style="padding:28px;color:{InkColor};font-size:14px;line-height:1.6;">{bodyHtml}</div>
            <div style="padding:20px 28px;background:{GroundColor};color:{MutedColor};font-size:12px;line-height:1.6;">{footerHtml}</div>
          </div>
        </div>
        """;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
