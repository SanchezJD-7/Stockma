using Stockma.Domain.Exceptions;

namespace Stockma.Application.Products.Exceptions;

public abstract class ProductException(string errorCode, string message)
    : DomainException(errorCode, message);

public sealed class SkuDuplicateException(string sku)
    : ProductException("PRODUCT_SKU_DUPLICATE", $"Ya existe un producto con el SKU '{sku}' en este tenant.");

public sealed class BarcodeDuplicateException(string barcode)
    : ProductException("PRODUCT_BARCODE_DUPLICATE", $"Ya existe un producto con el barcode '{barcode}' en este tenant.");

public sealed class SkuImmutableException(string currentSku, string attemptedSku)
    : ProductException(
        "PRODUCT_SKU_IMMUTABLE",
        $"El SKU es inmutable tras la creación: se intentó cambiar '{currentSku}' por '{attemptedSku}'.");

public sealed class ProductNotFoundException(Guid productId)
    : ProductException("PRODUCT_NOT_FOUND", $"No existe un producto con Id '{productId}' en este tenant.");

public sealed class BarcodeNotFoundException(string barcode)
    : ProductException("PRODUCT_NOT_FOUND", $"No existe un producto con barcode '{barcode}' en este tenant.");
