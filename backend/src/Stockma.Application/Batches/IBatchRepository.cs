using Stockma.Domain.Entities;

namespace Stockma.Application.Batches;

public interface IBatchRepository
{
    Task AddAsync(Batch batch, CancellationToken cancellationToken = default);
    Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Batch>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Batch> AdjustAsync(Guid batchId, int delta, CancellationToken cancellationToken = default);
}
