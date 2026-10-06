using Microsoft.EntityFrameworkCore;
using Stockma.Application.Tenants;
using Stockma.Domain.Entities;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Tenants;

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

    public async Task<TenantBranding?> GetBrandingAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return settings?.Branding;
    }

    public async Task<bool> RequireSecondFactorAsync(CancellationToken cancellationToken = default)
    {
        var settings = await context.TenantSettings
            .AsNoTracking()
            .Where(s => s.TenantId == context.CurrentTenantId)
            .Select(s => new { s.RequireSecondFactor })
            .SingleOrDefaultAsync(cancellationToken);

        return settings?.RequireSecondFactor ?? TenantSettings.DefaultRequireSecondFactor;
    }
}
