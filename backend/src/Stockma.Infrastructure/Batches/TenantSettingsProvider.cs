using Microsoft.EntityFrameworkCore;
using Stockma.Application.Batches;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Batches;

public sealed class TenantSettingsProvider(StockmaDbContext context) : ITenantSettingsProvider
{
    public async Task<ExpiryThresholds> GetExpiryThresholdsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return settings?.Thresholds ?? ExpiryThresholds.Default();
    }
}
