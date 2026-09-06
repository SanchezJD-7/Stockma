namespace Stockma.Application.Products;

public interface ISkuGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
