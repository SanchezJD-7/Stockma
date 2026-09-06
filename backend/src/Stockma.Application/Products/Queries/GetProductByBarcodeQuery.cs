using MediatR;
using Stockma.Application.Products.Exceptions;

namespace Stockma.Application.Products.Queries;

public sealed record GetProductByBarcodeQuery(string Barcode) : IRequest<ProductDto>;

public sealed class GetProductByBarcodeQueryHandler(IProductRepository products)
    : IRequestHandler<GetProductByBarcodeQuery, ProductDto>
{
    public async Task<ProductDto> Handle(
        GetProductByBarcodeQuery query,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByBarcodeAsync(query.Barcode, cancellationToken)
            ?? throw new BarcodeNotFoundException(query.Barcode);

        return ProductDto.From(product);
    }
}
