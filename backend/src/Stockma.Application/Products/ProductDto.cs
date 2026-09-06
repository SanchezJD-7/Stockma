using Stockma.Domain.Entities;
using Stockma.Domain.Enums;

namespace Stockma.Application.Products;
public sealed record ProductDto(
    Guid Id,
    string Sku,
    string? Barcode,
    string Name,
    ProductCategory Category,
    string? ActiveIngredient,
    string? Presentation,
    string? StorageConditions,
    string Currency)
{
    public static ProductDto From(Product product) =>
        new(
            product.Id,
            product.Sku,
            product.Barcode,
            product.Name,
            product.Category,
            product.ActiveIngredient,
            product.Presentation,
            product.StorageConditions,
            product.Currency);
}
