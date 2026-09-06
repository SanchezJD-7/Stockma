using Stockma.Domain.Common;
using Stockma.Domain.Enums;
using Stockma.Domain.Exceptions;

namespace Stockma.Domain.Entities;

public class Batch : ITenantEntity, IAggregateRoot
{
    private Batch()
    {
        LotNumber = string.Empty;
        LocationShelf = string.Empty;
    }

    public Batch(
        Guid tenantId,
        Guid productId,
        string lotNumber,
        DateOnly expirationDate,
        int initialQuantity,
        string locationShelf)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El TenantId no puede ser Guid.Empty.", nameof(tenantId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("El ProductId no puede ser Guid.Empty.", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(lotNumber))
        {
            throw new ArgumentException("El número de lote es obligatorio.", nameof(lotNumber));
        }

        if (string.IsNullOrWhiteSpace(locationShelf))
        {
            throw new ArgumentException("La ubicación es obligatoria.", nameof(locationShelf));
        }

        if (initialQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialQuantity),
                initialQuantity,
                "La cantidad inicial no puede ser negativa.");
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ProductId = productId;
        LotNumber = lotNumber.Trim();
        ExpirationDate = expirationDate;
        CurrentQuantity = initialQuantity;
        LocationShelf = locationShelf.Trim();
        Status = BatchStatus.Active;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProductId { get; private set; }

    public string LotNumber { get; private set; }

    public DateOnly ExpirationDate { get; private set; }

    public int CurrentQuantity { get; private set; }

    public string LocationShelf { get; private set; }
    public BatchStatus Status { get; private set; }
    public void Adjust(int delta)
    {
        if (delta == 0)
        {
            throw new ArgumentException("El ajuste debe ser distinto de cero.", nameof(delta));
        }

        var resulting = CurrentQuantity + delta;

        if (resulting < 0)
        {
            throw new NegativeStockException(CurrentQuantity, delta);
        }

        CurrentQuantity = resulting;
    }
}
