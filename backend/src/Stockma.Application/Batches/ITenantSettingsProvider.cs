using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Batches;

public interface ITenantSettingsProvider
{
    Task<ExpiryThresholds> GetExpiryThresholdsAsync(CancellationToken cancellationToken = default);
}
