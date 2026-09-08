using Stockma.Domain.Common;
using Stockma.Domain.Exceptions;

namespace Stockma.Domain.Entities;

public class DeviceOtp : ITenantEntity, IAggregateRoot
{
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(10);

    private DeviceOtp()
    {
        DeviceId = string.Empty;
        CodeHash = string.Empty;
    }

    public DeviceOtp(Guid tenantId, Guid userId, string deviceId, string codeHash, DateTimeOffset issuedAt, TimeSpan lifetime)
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

        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("El hash del código es obligatorio.", nameof(codeHash));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                "La vigencia del código debe ser positiva.");
        }

        if (lifetime > MaxLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                $"La vigencia del código no puede superar {MaxLifetime.TotalMinutes:0} minutos (NFR-004).");
        }

        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        UserId = userId;
        DeviceId = deviceId.Trim();
        CodeHash = codeHash;
        IssuedAt = issuedAt;
        ExpiresAt = issuedAt.Add(lifetime);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string DeviceId { get; private set; }
    public string CodeHash { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;
    public void Consume(DateTimeOffset usedAt)
    {
        if (!IsUsable(usedAt))
        {
            throw new OtpNotUsableException();
        }

        ConsumedAt = usedAt;
    }
}
