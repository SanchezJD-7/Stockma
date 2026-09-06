using MediatR;
using Stockma.Application.Common;
using Stockma.Application.Products.Exceptions;
using Stockma.Domain.Entities;
using Stockma.Domain.Enums;

namespace Stockma.Application.Products.Commands;

public sealed record RegisterProductCommand(
    string Name,
    ProductCategory Category,
    string? Barcode = null,
    string? Sku = null,
    string? ActiveIngredient = null,
    string? Presentation = null,
    string? StorageConditions = null,
    string Currency = Product.DefaultCurrency) : IRequest<ProductDto>;

public sealed class RegisterProductCommandHandler(
    IProductRepository products,
    ISkuGenerator skuGenerator,
    ITenantContext tenantContext) : IRequestHandler<RegisterProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(
        RegisterProductCommand command,
        CancellationToken cancellationToken)
    {
        var barcode = Blank(command.Barcode) ? null : command.Barcode!.Trim();

        if (barcode is not null
            && await products.BarcodeExistsAsync(barcode, cancellationToken))
        {
            throw new BarcodeDuplicateException(barcode);
        }

        string sku;
        if (Blank(command.Sku))
        {
            sku = await skuGenerator.NextAsync(cancellationToken);
        }
        else
        {
            sku = command.Sku!.Trim();

            if (await products.SkuExistsAsync(sku, cancellationToken))
            {
                throw new SkuDuplicateException(sku);
            }
        }

        var product = new Product(
            tenantId: tenantContext.TenantId,
            sku: sku,
            name: command.Name,
            category: command.Category,
            barcode: barcode,
            activeIngredient: command.ActiveIngredient,
            presentation: command.Presentation,
            storageConditions: command.StorageConditions,
            currency: command.Currency);

        await products.AddAsync(product, cancellationToken);

        return ProductDto.From(product);
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
}
