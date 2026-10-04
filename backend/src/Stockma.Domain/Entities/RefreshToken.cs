using Stockma.Domain.Common;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Domain.Entities;

public class RefreshToken : ITenantEntity, IAggregateRoot
{
    private RefreshToken()
    {
        DeviceId = string.Empty;
        TokenHash = string.Empty;
    }

    private RefreshToken(
        Guid tenantId,
        Guid userId,
        Guid familyId,
        string deviceId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset familyExpiresAt,
        int sessionIdleTimeoutMinutes)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El TenantId no puede ser Guid.Empty.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El UserId no puede ser Guid.Empty.", nameof(userId));
        }

        if (familyId == Guid.Empty)
        {
            throw new ArgumentException("El FamilyId no puede ser Guid.Empty.", nameof(familyId));
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("El identificador del dispositivo es obligatorio.", nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("El hash del token es obligatorio.", nameof(tokenHash));
        }

        if (sessionIdleTimeoutMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sessionIdleTimeoutMinutes),
                sessionIdleTimeoutMinutes,
                "El timeout de inactividad de la sesión debe ser positivo.");
        }

        if (familyExpiresAt <= issuedAt)
        {
            throw new ArgumentException(
                "El vencimiento absoluto de la familia debe ser posterior a la emisión.",
                nameof(familyExpiresAt));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        UserId = userId;
        FamilyId = familyId;
        DeviceId = DeviceIdentifier.Normalize(deviceId);
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        FamilyExpiresAt = familyExpiresAt;

        var idleExpiresAt = issuedAt.AddMinutes(sessionIdleTimeoutMinutes);
        ExpiresAt = idleExpiresAt < familyExpiresAt ? idleExpiresAt : familyExpiresAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string DeviceId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset FamilyExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static RefreshToken IssueForNewFamily(
        Guid tenantId,
        Guid userId,
        string deviceId,
        string tokenHash,
        DateTimeOffset issuedAt,
        int sessionIdleTimeoutMinutes,
        int refreshTokenLifetimeHours)
    {
        if (refreshTokenLifetimeHours <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshTokenLifetimeHours),
                refreshTokenLifetimeHours,
                "La vigencia absoluta de la familia debe ser positiva.");
        }

        return new RefreshToken(
            tenantId,
            userId,
            Guid.CreateVersion7(),
            deviceId,
            tokenHash,
            issuedAt,
            issuedAt.AddHours(refreshTokenLifetimeHours),
            sessionIdleTimeoutMinutes);
    }

    public RefreshToken CreateNextInFamily(string tokenHash, DateTimeOffset issuedAt, int sessionIdleTimeoutMinutes) =>
        new(TenantId, UserId, FamilyId, DeviceId, tokenHash, issuedAt, FamilyExpiresAt, sessionIdleTimeoutMinutes);

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && RevokedAt is null && ExpiresAt > now;

    public void Consume(DateTimeOffset consumedAt)
    {
        if (!IsUsable(consumedAt))
        {
            throw new RefreshTokenNotUsableException();
        }

        ConsumedAt = consumedAt;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        if (revokedAt < IssuedAt)
        {
            throw new ArgumentException(
                "La revocación no puede ser anterior a la emisión del token.",
                nameof(revokedAt));
        }

        RevokedAt = revokedAt;
    }
}
