using Microsoft.EntityFrameworkCore;

namespace Stockma.Infrastructure.Persistence;

public static class UserLocks
{
    public static async Task<T> RunLockedAsync<T>(
        this StockmaDbContext context,
        string scope,
        Guid subjectId,
        Func<Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            await AcquireAsync(context, scope, subjectId, cancellationToken);
            return await work();
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await AcquireAsync(context, scope, subjectId, cancellationToken);

        var result = await work();

        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private static Task AcquireAsync(
        StockmaDbContext context,
        string scope,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var key = $"{scope}:{subjectId}";

        return context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
            cancellationToken);
    }
}
