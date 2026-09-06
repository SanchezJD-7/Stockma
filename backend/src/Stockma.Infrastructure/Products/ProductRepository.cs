using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Application.Products;
using Stockma.Application.Products.Exceptions;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Products;

/// <summary>
/// Catálogo del tenant activo. Ninguna consulta filtra por tenant a mano: lo
/// hace el filtro global de EF Core, con RLS como respaldo (NFR-007).
/// </summary>
public sealed class ProductRepository(StockmaDbContext context) : IProductRepository
{
    private const string UniqueViolation = "23505";
    private const string SkuIndexName = "ix_products_tenant_sku";
    private const string BarcodeIndexName = "ix_products_tenant_barcode";

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        context.Products.Add(product);
        await SaveTranslatingUniqueViolationsAsync(product, cancellationToken);
    }

    public Task UpdateAsync(Product product, CancellationToken cancellationToken = default) =>
        SaveTranslatingUniqueViolationsAsync(product, cancellationToken);

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) =>
        context.Products.FirstOrDefaultAsync(p => p.Barcode == barcode, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default) =>
        context.Products.AnyAsync(p => p.Sku == sku, cancellationToken);

    public Task<bool> BarcodeExistsAsync(string barcode, CancellationToken cancellationToken = default) =>
        context.Products.AnyAsync(p => p.Barcode == barcode, cancellationToken);

    public async Task<IReadOnlyList<Product>> SearchByNameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var pattern = "%" + query.Trim() + "%";

        return await context.Products
            .FromSql(
                $"""
                SELECT * FROM products
                 WHERE name ILIKE {pattern}
                 ORDER BY similarity(name, {query}) DESC, name
                 LIMIT {limit}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private async Task SaveTranslatingUniqueViolationsAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: UniqueViolation } postgres)
        {
            throw postgres.ConstraintName switch
            {
                SkuIndexName => new SkuDuplicateException(product.Sku),
                BarcodeIndexName => new BarcodeDuplicateException(product.Barcode ?? string.Empty),
                _ => exception,
            };
        }
    }
}
