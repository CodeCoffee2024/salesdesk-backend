namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// Builds the absolute, client-facing URL for a document's public link
/// (`/view/{token}`), for embedding in places — reminder emails (TASK-025) — that,
/// unlike the frontend itself, don't already know their own origin. Implemented in
/// Infrastructure, backed by the deployed frontend's base URL (App:FrontendBaseUrl).
/// </summary>
public interface IPublicLinkBuilder
{
    string BuildDocumentUrl(Guid publicToken);
}
