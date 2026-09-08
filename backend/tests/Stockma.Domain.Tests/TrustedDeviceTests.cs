using FluentAssertions;
using Stockma.Domain.Entities;

namespace Stockma.Domain.Tests;

public class TrustedDeviceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset TrustedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = TrustedAt.AddDays(15);

    private static TrustedDevice CreateDevice() =>
        new(TenantId, UserId, "device-abc", "fingerprint-xyz", TrustedAt, ExpiresAt);

    [Fact]
    public void NewDevice_IsActive()
    {
        CreateDevice().IsActive(TrustedAt).Should().BeTrue();
    }

    [Fact]
    public void NewDevice_IsNotRevoked()
    {
        CreateDevice().RevokedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewDevice_RejectsBlankDeviceId(string deviceId)
    {
        var act = () => new TrustedDevice(TenantId, UserId, deviceId, "fp", TrustedAt, ExpiresAt);

        act.Should().Throw<ArgumentException>().WithParameterName("deviceId");
    }

    [Fact]
    public void NewDevice_RejectsEmptyTenantId()
    {
        var act = () => new TrustedDevice(Guid.Empty, UserId, "device-abc", "fp", TrustedAt, ExpiresAt);

        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Fact]
    public void NewDevice_RejectsEmptyUserId()
    {
        var act = () => new TrustedDevice(TenantId, Guid.Empty, "device-abc", "fp", TrustedAt, ExpiresAt);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public void NewDevice_RejectsExpiryBeforeTrustedAt()
    {
        var act = () => new TrustedDevice(
            TenantId,
            UserId,
            "device-abc",
            "fp",
            TrustedAt,
            TrustedAt.AddSeconds(-1));

        act.Should()
            .Throw<ArgumentException>("data-model: TrustedAt DEBE ser < ExpiresAt")
            .WithParameterName("expiresAt");
    }

    // ADR-007: activo = RevokedAt IS NULL AND ExpiresAt > now()

    [Fact]
    public void Device_IsActive_UntilTheInstantItExpires()
    {
        CreateDevice().IsActive(ExpiresAt.AddTicks(-1)).Should().BeTrue();
    }

    [Fact]
    public void ExpiredDevice_IsNotActive()
    {
        CreateDevice()
            .IsActive(ExpiresAt)
            .Should()
            .BeFalse("ADR-007: vencido deja de contar como activo, sin escribir RevokedAt");
    }

    [Fact]
    public void ExpiredDevice_KeepsRevokedAtNull()
    {
        var device = CreateDevice();

        device.IsActive(ExpiresAt.AddDays(1));

        device.RevokedAt
            .Should()
            .BeNull("ADR-007: el vencimiento NO escribe RevokedAt — revocado y vencido son cosas distintas");
    }

    [Fact]
    public void RevokedDevice_IsNotActive()
    {
        var device = CreateDevice();
        var revokedAt = TrustedAt.AddDays(1);

        device.Revoke(revokedAt);

        device.IsActive(revokedAt).Should().BeFalse();
        device.RevokedAt.Should().Be(revokedAt);
    }

    [Fact]
    public void RevokedDevice_StaysInactiveEvenBeforeExpiry()
    {
        var device = CreateDevice();
        device.Revoke(TrustedAt.AddDays(1));

        device.IsActive(TrustedAt.AddDays(2)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_RejectsAnInstantBeforeTrustedAt()
    {
        var device = CreateDevice();

        var act = () => device.Revoke(TrustedAt.AddSeconds(-1));

        act.Should()
            .Throw<ArgumentException>("data-model: TrustedAt DEBE ser <= RevokedAt cuando ambos existen")
            .WithParameterName("revokedAt");
    }

    [Fact]
    public void Revoke_IsIdempotent()
    {
        var device = CreateDevice();
        var firstRevocation = TrustedAt.AddDays(1);

        device.Revoke(firstRevocation);
        device.Revoke(TrustedAt.AddDays(2));

        device.RevokedAt
            .Should()
            .Be(firstRevocation, "T067: revocar dos veces no falla y conserva el primer instante");
    }
}
