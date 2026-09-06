using MediatR;
using Stockma.Application.Products.Exceptions;
using Stockma.Domain.Enums;

namespace Stockma.Application.Products.Commands;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    ProductCategory Category,
    string? Barcode = null,
    string? Sku = null,
    string? ActiveIngredient = null,
    string? Presentation = null,
    string? StorageConditions = null,
    string? Currency = null) : IRequest<ProductDto>;

public sealed class UpdateProductCommandHandler(IProductRepository products)
    : IRequestHandler<UpdateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(command.Id, cancellationToken) ?? throw new ProductNotFoundException(command.Id);
        if (!string.IsNullOrWhiteSpace(command.Sku)
            && !string.Equals(command.Sku.Trim(), product.Sku, StringComparison.Ordinal))
        {
            throw new SkuImmutableException(product.Sku, command.Sku.Trim());
        }

        var barcode = string.IsNullOrWhiteSpace(command.Barcode) ? null : command.Barcode.Trim();
        if (barcode is not null
            && !string.Equals(barcode, product.Barcode, StringComparison.Ordinal)
            && await products.BarcodeExistsAsync(barcode, cancellationToken))
        {
            throw new BarcodeDuplicateException(barcode);
        }

        product.Update(
            name: command.Name,
            category: command.Category,
            barcode: barcode,
            activeIngredient: command.ActiveIngredient,
            presentation: command.Presentation,
            storageConditions: command.StorageConditions,
            currency: string.IsNullOrWhiteSpace(command.Currency) ? product.Currency : command.Currency);

        await products.UpdateAsync(product, cancellationToken);

        return ProductDto.From(product);
    }
}
