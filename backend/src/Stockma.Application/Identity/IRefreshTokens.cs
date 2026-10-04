using Stockma.Domain.Entities;

namespace Stockma.Application.Identity;

public interface IRefreshTokens
{
    Task<RefreshTokenLookup?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    Task RevokeByDeviceAsync(Guid userId, IReadOnlyCollection<string> deviceIds, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenLookup(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    Guid FamilyId,
    string DeviceId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset FamilyExpiresAt,
    DateTimeOffset? ConsumedAt,
    DateTimeOffset? RevokedAt);
