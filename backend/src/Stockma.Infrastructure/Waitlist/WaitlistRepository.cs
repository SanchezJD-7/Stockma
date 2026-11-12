using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Application.Waitlist;
using Stockma.Application.Waitlist.Exceptions;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Persistence.Configurations;

namespace Stockma.Infrastructure.Waitlist;

public sealed class WaitlistRepository(StockmaDbContext context) : IWaitlistRepository
{
    private const string UniqueViolation = "23505";

    public async Task AddAsync(WaitlistEntry entry, CancellationToken cancellationToken = default)
    {
        await context.WaitlistEntries.AddAsync(entry, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: UniqueViolation } postgres)
        {
            if (postgres.ConstraintName == WaitlistEntryConfiguration.EmailIndexName)
            {
                throw new WaitlistEmailAlreadyRegisteredException(entry.Email);
            }

            throw;
        }
    }
}
