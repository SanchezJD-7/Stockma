using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Tenants;

public interface ITenantSettingsProvider
{
    Task<ExpiryThresholds> GetExpiryThresholdsAsync(CancellationToken cancellationToken = default);
    Task<TenantBranding?> GetBrandingAsync(CancellationToken cancellationToken = default);
}
