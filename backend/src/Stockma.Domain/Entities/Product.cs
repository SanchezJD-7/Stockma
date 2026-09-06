using Stockma.Domain.Common;
using Stockma.Domain.Enums;

namespace Stockma.Domain.Entities;

public class Product : ITenantEntity, IAggregateRoot
{
    public const string DefaultCurrency = "COP";

    private Product()
    {
        Sku = string.Empty;
        Name = string.Empty;
        Currency = DefaultCurrency;
    }

    public Product(
        Guid tenantId,
        string sku,
        string name,
        ProductCategory category,
        string? barcode,
        string? activeIngredient = null,
        string? presentation = null,
        string? storageConditions = null,
        string currency = DefaultCurrency)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El TenantId no puede ser Guid.Empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("El SKU es obligatorio.", nameof(sku));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del producto es obligatorio.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("El código de moneda es obligatorio.", nameof(currency));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        Sku = sku.Trim();
        Name = name.Trim();
        Category = category;
        Barcode = Normalize(barcode);
        ActiveIngredient = Normalize(activeIngredient);
        Presentation = Normalize(presentation);
        StorageConditions = Normalize(storageConditions);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Sku { get; private set; }
    public string? Barcode { get; private set; }
    public string Name { get; private set; }
    public ProductCategory Category { get; private set; }
    public string? ActiveIngredient { get; private set; }
    public string? Presentation { get; private set; }
    public string? StorageConditions { get; private set; }
    public string Currency { get; private set; }
    public void Update(
        string name,
        ProductCategory category,
        string? barcode,
        string? activeIngredient,
        string? presentation,
        string? storageConditions,
        string currency)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del producto es obligatorio.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("El código de moneda es obligatorio.", nameof(currency));
        }

        Name = name.Trim();
        Category = category;
        Barcode = Normalize(barcode);
        ActiveIngredient = Normalize(activeIngredient);
        Presentation = Normalize(presentation);
        StorageConditions = Normalize(storageConditions);
        Currency = currency.Trim().ToUpperInvariant();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
