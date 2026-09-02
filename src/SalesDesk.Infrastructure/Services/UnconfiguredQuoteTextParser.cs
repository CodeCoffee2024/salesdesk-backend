using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Infrastructure.Services;

/// <summary>Registered in place of GeminiQuoteTextParser when no Gemini:ApiKey is configured (see DependencyInjection). Unlike LogEmailSender there's no meaningful no-op result for a parse request, so this fails clearly instead of silently returning empty data the caller might mistake for "nothing found in the text".</summary>
public sealed class UnconfiguredQuoteTextParser : IQuoteTextParser
{
    public Task<ParsedQuoteText> ParseAsync(string rawText, CancellationToken cancellationToken) =>
        throw new AiParsingUnavailableException("AI text parsing isn't configured on this server. Set Gemini:ApiKey to enable it.");
}
