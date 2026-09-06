using Microsoft.EntityFrameworkCore;
using Stockma.Application.Batches;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Batches;

public static class BatchConcurrency
{
    public const int DefaultMaxRetries = 1;
}

public sealed class BatchRepository(
    StockmaDbContext context,
    int maxRetries = BatchConcurrency.DefaultMaxRetries)
    : IBatchRepository
{
    public async Task AddAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        context.Batches.Add(batch);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Batches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Batch>> GetByProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        await context.Batches
            .Where(b => b.ProductId == productId)
            .OrderBy(b => b.ExpirationDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<Batch> AdjustAsync(
        Guid batchId,
        int delta,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            context.ChangeTracker.Clear();

            var batch = await context.Batches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken)
                ?? throw new BatchNotFoundException(batchId);
            batch.Adjust(delta);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return batch;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            { }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException(nameof(Batch), batchId);
            }
        }
    }
}
