using Stockma.Domain.Common;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Entities;

public class TenantSettings : ITenantEntity
{
    public const int DefaultMaxTrustedDevices = 2;
    public const int InitialSkuNumber = 1;

    private TenantSettings()
    {
        Thresholds = ExpiryThresholds.Default();
        NextSkuNumber = InitialSkuNumber;
    }

    public TenantSettings(Guid tenantId, int maxTrustedDevices, ExpiryThresholds thresholds)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El TenantId no puede ser Guid.Empty.", nameof(tenantId));
        }

        if (maxTrustedDevices < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTrustedDevices),
                maxTrustedDevices,
                "MaxTrustedDevices debe ser mayor o igual a 1.");
        }

        TenantId = tenantId;
        MaxTrustedDevices = maxTrustedDevices;
        NextSkuNumber = InitialSkuNumber;
        Thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
    }

    public Guid TenantId { get; private set; }
    public int MaxTrustedDevices { get; private set; }
    public ExpiryThresholds Thresholds { get; private set; }
    public int NextSkuNumber { get; private set; }
    public static TenantSettings CreateDefault(Guid tenantId) =>
        new(tenantId, DefaultMaxTrustedDevices, ExpiryThresholds.Default());
}
