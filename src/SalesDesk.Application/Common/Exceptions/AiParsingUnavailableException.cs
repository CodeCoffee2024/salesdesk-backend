namespace SalesDesk.Application.Common.Exceptions;

/// <summary>Thrown when AI text parsing (TASK-033) is invoked but no provider API key is configured. Mapped to 503 by GlobalExceptionHandler.</summary>
public sealed class AiParsingUnavailableException(string message) : Exception(message);
