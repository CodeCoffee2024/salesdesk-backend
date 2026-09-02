namespace SalesDesk.Application.Common.Exceptions;

/// <summary>Thrown when a configured AI provider call fails or returns something the parser can't use (bad response, malformed JSON, timeout). Mapped to 502 by GlobalExceptionHandler, distinct from AiParsingUnavailableException's 503 (not configured at all).</summary>
public sealed class AiParsingFailedException(string message, Exception? inner = null) : Exception(message, inner);
