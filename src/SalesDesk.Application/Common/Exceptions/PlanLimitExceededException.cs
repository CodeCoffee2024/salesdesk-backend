namespace SalesDesk.Application.Common.Exceptions;

public sealed class PlanLimitExceededException(string message) : Exception(message);
