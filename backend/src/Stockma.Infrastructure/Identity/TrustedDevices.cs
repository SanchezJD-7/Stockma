using Microsoft.EntityFrameworkCore;
using Stockma.Application.Identity;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class TrustedDevices(
    StockmaDbContext context,
    TimeProvider timeProvider) : ITrustedDevices
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(15);

    public async Task<bool> IsTrustedAsync(
        Guid userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        return await context.TrustedDevices.AnyAsync(
            device => device.UserId == userId
                && device.DeviceId == deviceId
                && device.RevokedAt == null
                && device.ExpiresAt > now,
            cancellationToken);
    }

    public async Task<bool> TryTrustAsync(
        Guid userId,
        string deviceId,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        if (await IsTrustedAsync(userId, deviceId, cancellationToken))
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
}
