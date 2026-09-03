using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>
/// Reads the deployed frontend's base URL (App:FrontendBaseUrl — see
/// docs/research/TASK-DEPLOYMENT.md) and builds the same `/view/{token}` route the
/// Angular app itself routes to, so a server-generated artifact (a reminder email,
/// TASK-025) can link straight to the public document view. Also reads the API's
/// own base URL (App:ApiBaseUrl, TASK-039) for the one link that isn't a frontend
/// route at all — the GCash admin-approval link hits the API directly.
/// </summary>
public sealed class PublicLinkBuilder(string frontendBaseUrl, string apiBaseUrl) : IPublicLinkBuilder
{
    public string BuildDocumentUrl(Guid publicToken) =>
        $"{frontendBaseUrl.TrimEnd('/')}/view/{publicToken:D}";

    public string BuildResetPasswordUrl(string rawToken) =>
        $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";

    public string BuildDocumentPreviewUrl(Guid documentId) =>
        $"{frontendBaseUrl.TrimEnd('/')}/documents/{documentId:D}/preview";

    public string BuildVerifyEmailUrl(string rawToken) =>
        $"{frontendBaseUrl.TrimEnd('/')}/auth/verify-email?token={Uri.EscapeDataString(rawToken)}";

    public string BuildApproveGCashSubscriptionUrl(string rawToken) =>
        $"{apiBaseUrl.TrimEnd('/')}/api/admin/subscriptions/approve?token={Uri.EscapeDataString(rawToken)}";
}
