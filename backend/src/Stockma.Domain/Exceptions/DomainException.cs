namespace Stockma.Domain.Exceptions;

public abstract class DomainException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
public sealed class ValidationFailedException(string message)
    : DomainException("VALIDATION_FAILED", message);

public sealed class NegativeStockException(int currentQuantity, int delta)
    : DomainException(
        "BATCH_NEGATIVE_STOCK",
        $"El ajuste de {delta:+#;-#;0} dejaría el stock en {currentQuantity + delta}, "
        + $"y el stock no puede ser negativo. Stock actual: {currentQuantity}. El ajuste se rechaza por completo.");

public sealed class ConcurrencyConflictException(string entity, Guid id)
    : DomainException(
        "CONCURRENCY_CONFLICT",
        $"Otro proceso modificó {entity} '{id}' durante la operación. Se reintentó una vez sin éxito.");

public sealed class ProductNotFoundForBatchException(Guid productId)
    : DomainException("BATCH_PRODUCT_NOT_FOUND", $"No existe un producto con Id '{productId}' en este tenant.");

public sealed class BatchNotFoundException(Guid batchId)
    : DomainException("BATCH_NOT_FOUND", $"No existe un lote con Id '{batchId}' en este tenant.");

public sealed class OtpNotUsableException()
    : DomainException("AUTH_OTP_REJECTED", "El código es incorrecto o ya no es válido.");

public sealed class PhoneNotEnrolledException()
    : DomainException(
        "AUTH_PHONE_NOT_ENROLLED",
        "El usuario no tiene un celular cargado para recibir el código. Un admin del tenant debe cargarlo.");

public sealed class InvalidCredentialsException()
    : DomainException("AUTH_INVALID_CREDENTIALS", "Email o contraseña incorrectos.");

public sealed class EmailAlreadyRegisteredException()
    : DomainException("AUTH_EMAIL_DUPLICATE", "Ese email ya está registrado en la plataforma.");

public sealed class TenantNotFoundException(Guid tenantId)
    : DomainException("TENANT_NOT_FOUND", $"No existe un tenant con Id '{tenantId}'.");

public sealed class TenantAlreadyBootstrappedException(Guid tenantId)
    : DomainException(
        "TENANT_ALREADY_BOOTSTRAPPED",
        $"El tenant '{tenantId}' ya tiene usuarios; el bootstrap sólo corre sobre un tenant vacío.");

public sealed class RefreshTokenNotUsableException()
    : DomainException("AUTH_REFRESH_REJECTED", "El refresh token es inválido, ya fue usado o ya no es utilizable.");
