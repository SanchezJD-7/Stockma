using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Stockma.Domain.Common;

namespace Stockma.Infrastructure.Persistence.Interceptors;

public sealed class TenantImmutabilityInterceptor : SaveChangesInterceptor
{
    private const string TenantIdPropertyName = nameof(ITenantEntity.TenantId);
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Validate(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Validate(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Validate(DbContextEventData eventData)
    {
        var context = eventData.Context;
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified || entry.Entity is not ITenantEntity)
            {
                continue;
            }

            PropertyEntry property;
            try
            {
                property = entry.Property(TenantIdPropertyName);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (property.IsModified)
            {
                throw new InvalidOperationException(
                    $"El TenantId de '{entry.Metadata.ClrType.Name}' es inmutable tras la creación (FR-004). " +
                    $"Se intentó cambiar de '{property.OriginalValue}' a '{property.CurrentValue}'.");
            }
        }
    }
}
