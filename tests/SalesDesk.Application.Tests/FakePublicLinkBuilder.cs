using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Tests;

public sealed class FakePublicLinkBuilder : IPublicLinkBuilder
{
    public string BuildDocumentUrl(Guid publicToken) => $"https://app.example.test/view/{publicToken:D}";

    public string BuildResetPasswordUrl(string rawToken) => $"https://app.example.test/reset-password?token={rawToken}";

    public string BuildDocumentPreviewUrl(Guid documentId) => $"https://app.example.test/documents/{documentId:D}/preview";
}
