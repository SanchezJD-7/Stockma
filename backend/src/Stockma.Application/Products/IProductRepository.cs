using Stockma.Domain.Entities;

namespace Stockma.Application.Products;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default);
    Task<bool> BarcodeExistsAsync(string barcode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> SearchByNameAsync(string query, int limit, CancellationToken cancellationToken = default);
}
