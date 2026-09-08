using Stockma.Domain.Common;

namespace Stockma.Domain.Entities;

public class TrustedDevice : ITenantEntity, IAggregateRoot
{
    private TrustedDevice()
    {
        DeviceId = string.Empty;
        Fingerprint = string.Empty;
    }

    public TrustedDevice(
        Guid tenantId,
        Guid userId,
        string deviceId,
        string fingerprint,
        DateTimeOffset trustedAt,
        DateTimeOffset expiresAt)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El TenantId no puede ser Guid.Empty.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El UserId no puede ser Guid.Empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("El identificador del dispositivo es obligatorio.", nameof(deviceId));
        }

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new ArgumentException("La huella del dispositivo es obligatoria.", nameof(fingerprint));
        }

        if (expiresAt <= trustedAt)
        {
            throw new ArgumentException(
                "El vencimiento debe ser posterior al momento en que el dispositivo pasó a ser confiable.",
                nameof(expiresAt));
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        UserId = userId;
        DeviceId = deviceId.Trim();
        Fingerprint = fingerprint.Trim();
        TrustedAt = trustedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string DeviceId { get; private set; }
    public string Fingerprint { get; private set; }
    public DateTimeOffset TrustedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
    public void Revoke(DateTimeOffset revokedAt)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        if (revokedAt < TrustedAt)
        {
            throw new ArgumentException(
                "La revocación no puede ser anterior al momento en que el dispositivo pasó a ser confiable.",
                nameof(revokedAt));
        }

        RevokedAt = revokedAt;
    }
}
