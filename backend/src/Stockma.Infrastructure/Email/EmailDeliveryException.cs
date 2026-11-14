namespace Stockma.Infrastructure.Email;

public sealed class EmailDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
