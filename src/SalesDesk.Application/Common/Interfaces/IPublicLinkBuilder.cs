namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// Builds absolute, client-facing frontend URLs for places — reminder emails
/// (TASK-025), password-reset emails — that, unlike the frontend itself, don't
/// already know their own origin. Implemented in Infrastructure, backed by the
/// deployed frontend's base URL (App:FrontendBaseUrl).
/// </summary>
public interface IPublicLinkBuilder
{
    string BuildDocumentUrl(Guid publicToken);

    string BuildResetPasswordUrl(string rawToken);
}
