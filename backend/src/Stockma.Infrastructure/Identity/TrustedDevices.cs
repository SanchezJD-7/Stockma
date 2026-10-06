using Microsoft.EntityFrameworkCore;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Queries;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class TrustedDevices(
    StockmaDbContext context,
    TimeProvider timeProvider,
    IRefreshTokens refreshTokens) : ITrustedDevices
{
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

    public async Task<IReadOnlyList<TrustedDeviceInfo>> GetDevicesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        await EnsureUserExistsAsync(userId, cancellationToken);

        var devices = await context.TrustedDevices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.TrustedAt)
            .Select(d => new TrustedDeviceInfo(
                d.Id,
                d.UserId,
                d.DeviceId,
                d.TrustedAt,
                d.LastUsedAt,
                d.ExpiresAt,
                d.RevokedAt == null && d.ExpiresAt > now,
                d.RevokedAt))
            .ToListAsync(cancellationToken);

        return devices;
    }

    public async Task RevokeAsync(Guid userId, Guid? deviceId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var query = context.TrustedDevices.Where(d => d.UserId == userId);

        if (deviceId.HasValue)
            query = query.Where(d => d.Id == deviceId.Value);

        var devices = await query.ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            if (deviceId.HasValue)
            {
                throw new TrustedDeviceNotFoundException(deviceId.Value);
            }

            await EnsureUserExistsAsync(userId, cancellationToken);
            return;
        }

        foreach (var device in devices)
            device.Revoke(now);

        await refreshTokens.RevokeByDeviceAsync(
            userId,
            devices.Select(device => device.DeviceId).Distinct().ToList(),
            now,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
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

        var settings = await context.TenantSettings
            .Where(s => s.TenantId == context.CurrentTenantId)
            .Select(s => new { s.MaxTrustedDevices, s.TrustedDeviceLifetimeDays })
            .SingleOrDefaultAsync(cancellationToken)
            ?? new { MaxTrustedDevices = TenantSettings.DefaultMaxTrustedDevices, TrustedDeviceLifetimeDays = TenantSettings.DefaultTrustedDeviceLifetimeDays };

        var active = await context.TrustedDevices.CountAsync(
            device => device.UserId == userId && device.RevokedAt == null && device.ExpiresAt > now,
            cancellationToken);

        if (active >= settings.MaxTrustedDevices)
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
                now.AddDays(settings.TrustedDeviceLifetimeDays)));

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

    private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var exists = await context.Users.AnyAsync(user => user.Id == userId, cancellationToken);

        if (!exists)
        {
            throw new UserNotFoundException(userId);
        }
    }
}
