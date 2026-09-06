using Stockma.Domain.Entities;
using Stockma.Domain.Enums;

namespace Stockma.Application.Batches;

public sealed record BatchDto(
    Guid Id,
    Guid ProductId,
    string LotNumber,
    DateOnly ExpirationDate,
    int CurrentQuantity,
    string LocationShelf,
    BatchStatus Status,
    SemaphoreColor? SemaphoreColor = null)
{
    public static BatchDto From(Batch batch, SemaphoreColor? semaphoreColor = null) =>
        new(
            batch.Id,
            batch.ProductId,
            batch.LotNumber,
            batch.ExpirationDate,
            batch.CurrentQuantity,
            batch.LocationShelf,
            batch.Status,
            semaphoreColor);
}
