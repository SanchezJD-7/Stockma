namespace Stockma.Application.Identity;

public sealed class SmsDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
