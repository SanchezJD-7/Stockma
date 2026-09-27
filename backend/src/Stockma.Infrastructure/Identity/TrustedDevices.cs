using Microsoft.EntityFrameworkCore;
using Stockma.Application.Identity;
using Stockma.Domain.Entities;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class TrustedDevices(
    StockmaDbContext context,
    TimeProvider timeProvider) : ITrustedDevices
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(15);

    private const string LockScope = "trusted_devices";

    public Task<bool> IsTrustedAsync(
        Guid userId,
        string deviceId,
        CancellationToken cancellationToken = default) =>
        IsActiveAsync(userId, DeviceIdentifier.Normalize(deviceId), timeProvider.GetUtcNow(), cancellationToken);

    public Task<bool> TryTrustAsync(
        Guid userId,
        string deviceId,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var normalizedDeviceId = DeviceIdentifier.Normalize(deviceId);

        return context.RunLockedAsync(
            LockScope,
            userId,
            () => TryTrustLockedAsync(userId, normalizedDeviceId, fingerprint, cancellationToken),
            cancellationToken);
    }

    private async Task<bool> TryTrustLockedAsync(
        Guid userId,
        string deviceId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        if (await IsActiveAsync(userId, deviceId, now, cancellationToken))
        {
            return true;
        }

        var maxTrustedDevices = await context.TenantSettings
            .Where(settings => settings.TenantId == context.CurrentTenantId)
            .Select(settings => (int?)settings.MaxTrustedDevices)
            .SingleOrDefaultAsync(cancellationToken)
            ?? TenantSettings.DefaultMaxTrustedDevices;

        var active = await context.TrustedDevices.CountAsync(
            device => device.UserId == userId && device.RevokedAt == null && device.ExpiresAt > now,
            cancellationToken);

        if (active >= maxTrustedDevices)
        {
            return false;
        }

        context.TrustedDevices.Add(
            new TrustedDevice(
                context.CurrentTenantId,
                userId,
                deviceId,
                fingerprint,
                now,
                now.Add(DefaultLifetime)));

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task<bool> IsActiveAsync(
        Guid userId,
        string deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        context.TrustedDevices.AnyAsync(
            device => device.UserId == userId
                && device.DeviceId == deviceId
                && device.RevokedAt == null
                && device.ExpiresAt > now,
            cancellationToken);
}
