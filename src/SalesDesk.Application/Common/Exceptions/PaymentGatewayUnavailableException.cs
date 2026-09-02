namespace SalesDesk.Application.Common.Exceptions;

public sealed class PaymentGatewayUnavailableException(string message) : Exception(message);
