using MediatR;

namespace Stockma.Application.Products.Queries;

public static class ProductSearchLimits
{
    public const int Default = 20;
    public const int Max = 100;
}

public sealed record SearchProductsQuery(string Query, int Limit = ProductSearchLimits.Default)
    : IRequest<IReadOnlyList<ProductDto>>;

public sealed class SearchProductsQueryHandler(IProductRepository products)
    : IRequestHandler<SearchProductsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<IReadOnlyList<ProductDto>> Handle(SearchProductsQuery query, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit, 1, ProductSearchLimits.Max);
        var matches = await products.SearchByNameAsync(query.Query, limit, cancellationToken);
        return [.. matches.Select(ProductDto.From)];
    }
}
