using Stockma.Domain.Exceptions;

namespace Stockma.Application.Waitlist.Exceptions;

public abstract class WaitlistException(string errorCode, string message)
    : DomainException(errorCode, message);

public sealed class WaitlistEmailAlreadyRegisteredException(string email)
    : WaitlistException(
        "WAITLIST_EMAIL_DUPLICATE",
        $"El correo '{email}' ya está en la lista de espera.");
