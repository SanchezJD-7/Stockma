using Stockma.Domain.Common;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Entities;

public class TenantSettings : ITenantEntity
{
    public const int DefaultMaxTrustedDevices = 2;
    public const int InitialSkuNumber = 1;
    public const int DefaultRefreshTokenLifetimeHours = 8;
    public const int DefaultSessionIdleTimeoutMinutes = 30;
    public const int DefaultTrustedDeviceLifetimeDays = 15;
    public const int AccessTokenLifetimeMinutes = 15;

    private TenantSettings()
    {
        Thresholds = ExpiryThresholds.Default();
        NextSkuNumber = InitialSkuNumber;
    }

    public TenantSettings(
        Guid tenantId,
        int maxTrustedDevices,
        ExpiryThresholds thresholds,
        int refreshTokenLifetimeHours,
        int sessionIdleTimeoutMinutes,
        int trustedDeviceLifetimeDays)
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

        if (refreshTokenLifetimeHours < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshTokenLifetimeHours),
                refreshTokenLifetimeHours,
                "RefreshTokenLifetimeHours debe ser mayor o igual a 1.");
        }

        if (sessionIdleTimeoutMinutes <= AccessTokenLifetimeMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sessionIdleTimeoutMinutes),
                sessionIdleTimeoutMinutes,
                $"SessionIdleTimeoutMinutes debe ser mayor que la vigencia del access token ({AccessTokenLifetimeMinutes} min, ADR-019).");
        }

        if (trustedDeviceLifetimeDays < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trustedDeviceLifetimeDays),
                trustedDeviceLifetimeDays,
                "TrustedDeviceLifetimeDays debe ser mayor o igual a 1.");
        }

        if (sessionIdleTimeoutMinutes > refreshTokenLifetimeHours * 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sessionIdleTimeoutMinutes),
                sessionIdleTimeoutMinutes,
                "SessionIdleTimeoutMinutes no puede superar RefreshTokenLifetimeHours x 60.");
        }

        TenantId = tenantId;
        MaxTrustedDevices = maxTrustedDevices;
        NextSkuNumber = InitialSkuNumber;
        Thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
        RefreshTokenLifetimeHours = refreshTokenLifetimeHours;
        SessionIdleTimeoutMinutes = sessionIdleTimeoutMinutes;
        TrustedDeviceLifetimeDays = trustedDeviceLifetimeDays;
    }

    public Guid TenantId { get; private set; }
    public int MaxTrustedDevices { get; private set; }
    public ExpiryThresholds Thresholds { get; private set; }
    public int NextSkuNumber { get; private set; }
    public TenantBranding? Branding { get; private set; }
    public int RefreshTokenLifetimeHours { get; private set; } = DefaultRefreshTokenLifetimeHours;
    public int SessionIdleTimeoutMinutes { get; private set; } = DefaultSessionIdleTimeoutMinutes;
    public int TrustedDeviceLifetimeDays { get; private set; } = DefaultTrustedDeviceLifetimeDays;
    public void UpdateBranding(TenantBranding? branding) => Branding = branding;
    public static TenantSettings CreateDefault(Guid tenantId) =>
        new(
            tenantId,
            DefaultMaxTrustedDevices,
            ExpiryThresholds.Default(),
            DefaultRefreshTokenLifetimeHours,
            DefaultSessionIdleTimeoutMinutes,
            DefaultTrustedDeviceLifetimeDays);
}
