using Microsoft.EntityFrameworkCore;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Application.Tenants;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Identity;

public sealed class RefreshTokenService(
    StockmaDbContext context,
    IRefreshTokens tokens,
    IUserAccounts accounts,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : IRefreshTokenService
{
    private const string FamilyLockScope = "refresh_tokens";

    public async Task<string> IssueNewFamilyAsync(
        Guid tenantId,
        Guid userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        tenantContext.Set(tenantId);

        var settings = await LoadSettingsAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var plainToken = RefreshTokenMaterial.GenerateToken();

        await tokens.AddAsync(
            RefreshToken.IssueForNewFamily(
                tenantId,
                userId,
                deviceId,
                RefreshTokenMaterial.Hash(plainToken),
                now,
                settings.SessionIdleTimeoutMinutes,
                settings.RefreshTokenLifetimeHours),
            cancellationToken);

        return plainToken;
    }

    public async Task<RefreshTokenSession> RotateAsync(
        string presentedToken,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var lookup = await FindByHashAsync(presentedToken, cancellationToken);

        if (lookup is null)
        {
            throw new RefreshTokenNotUsableException();
        }

        tenantContext.Set(lookup.TenantId);

        return await context.RunLockedAsync(
            FamilyLockScope,
            lookup.FamilyId,
            async () => await RotateLockedAsync(lookup, deviceId, cancellationToken),
            cancellationToken);
    }

    public async Task RevokeFamilyAsync(string presentedToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            return;
        }

        var lookup = await FindByHashAsync(presentedToken, cancellationToken);

        if (lookup is null)
        {
            return;
        }

        tenantContext.Set(lookup.TenantId);

        await tokens.RevokeFamilyAsync(lookup.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task<RefreshTokenSession> RotateLockedAsync(
        RefreshTokenLookup lookup,
        string deviceId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var token = await context.RefreshTokens
            .SingleOrDefaultAsync(candidate => candidate.Id == lookup.Id, cancellationToken)
            ?? throw new RefreshTokenNotUsableException();

        if (token.ConsumedAt is not null || token.RevokedAt is not null)
        {
            await RevokeFamilyInternalAsync(lookup.FamilyId, now, cancellationToken);
            throw new RefreshTokenNotUsableException();
        }

        if (token.ExpiresAt <= now)
        {
            throw new RefreshTokenNotUsableException();
        }

        if (token.DeviceId != deviceId)
        {
            await RevokeFamilyInternalAsync(lookup.FamilyId, now, cancellationToken);
            throw new RefreshTokenNotUsableException();
        }

        var identity = await accounts.FindByIdAsync(token.UserId, cancellationToken);

        if (identity is null)
        {
            await RevokeFamilyInternalAsync(lookup.FamilyId, now, cancellationToken);
            throw new RefreshTokenNotUsableException();
        }

        var settings = await LoadSettingsAsync(cancellationToken);

        token.Consume(now);

        await TouchTrustedDeviceAsync(lookup.TenantId, token.UserId, token.DeviceId, now, cancellationToken);

        var plainToken = RefreshTokenMaterial.GenerateToken();
        var next = token.CreateNextInFamily(
            RefreshTokenMaterial.Hash(plainToken),
            now,
            settings.SessionIdleTimeoutMinutes);

        context.RefreshTokens.Add(next);
        await context.SaveChangesAsync(cancellationToken);

        return new RefreshTokenSession(plainToken, token.UserId, token.TenantId, identity.Roles);
    }

    private async Task TouchTrustedDeviceAsync(
        Guid tenantId,
        Guid userId,
        string deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var device = await context.TrustedDevices
            .Where(candidate => candidate.TenantId == tenantId
                && candidate.UserId == userId
                && candidate.DeviceId == deviceId
                && candidate.RevokedAt == null
                && candidate.ExpiresAt > now)
            .OrderByDescending(candidate => candidate.TrustedAt)
            .FirstOrDefaultAsync(cancellationToken);

        device?.Touch(now);
    }

    private async Task RevokeFamilyInternalAsync(
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken) =>
        await tokens.RevokeFamilyAsync(familyId, revokedAt, cancellationToken);

    private async Task<RefreshTokenLookup?> FindByHashAsync(string presentedToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            return null;
        }

        return await tokens.FindByHashAsync(RefreshTokenMaterial.Hash(presentedToken), cancellationToken);
    }

    private async Task<TenantSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return settings ?? TenantSettings.CreateDefault(tenantContext.TenantId);
    }
}
