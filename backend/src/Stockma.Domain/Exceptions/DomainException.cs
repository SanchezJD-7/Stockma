namespace Stockma.Domain.Exceptions;

public abstract class DomainException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
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

/// <summary>
/// Un único errorCode para "OTP errado" y "OTP vencido": distinguirlos le diría al atacante
/// si el código existía, que es información que no debe filtrarse (ADR-002).
/// </summary>
public sealed class OtpNotUsableException()
    : DomainException("AUTH_OTP_REJECTED", "El código es incorrecto o ya no es válido.");
