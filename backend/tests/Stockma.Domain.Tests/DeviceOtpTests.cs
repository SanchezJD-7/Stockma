using FluentAssertions;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;

namespace Stockma.Domain.Tests;

public class DeviceOtpTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset IssuedAt = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private static DeviceOtp CreateOtp(TimeSpan? lifetime = null) =>
        new(TenantId, UserId, "device-abc", "hash-del-codigo", IssuedAt, lifetime ?? TenMinutes);

    [Fact]
    public void NewOtp_IsNotConsumed()
    {
        CreateOtp().ConsumedAt.Should().BeNull();
    }

    [Fact]
    public void NewOtp_ExpiresAtIssuedAtPlusLifetime()
    {
        CreateOtp().ExpiresAt.Should().Be(IssuedAt.Add(TenMinutes));
    }

    [Fact]
    public void NewOtp_NeverStoresTheCodeInPlainText()
    {
        var otp = CreateOtp();

        otp.GetType()
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .NotContain(
                "Code",
                "NFR-004: el OTP se persiste hasheado; la entidad sólo conoce el hash");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewOtp_RejectsBlankCodeHash(string codeHash)
    {
        var act = () => new DeviceOtp(TenantId, UserId, "device-abc", codeHash, IssuedAt, TenMinutes);

        act.Should().Throw<ArgumentException>().WithParameterName("codeHash");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewOtp_RejectsBlankDeviceId(string deviceId)
    {
        var act = () => new DeviceOtp(TenantId, UserId, deviceId, "hash", IssuedAt, TenMinutes);

        act.Should().Throw<ArgumentException>().WithParameterName("deviceId");
    }

    [Fact]
    public void NewOtp_RejectsEmptyTenantId()
    {
        var act = () => new DeviceOtp(Guid.Empty, UserId, "device-abc", "hash", IssuedAt, TenMinutes);

        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Fact]
    public void NewOtp_RejectsNonPositiveLifetime()
    {
        var act = () => CreateOtp(TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("lifetime");
    }

    [Fact]
    public void NewOtp_RejectsLifetimeBeyondTenMinutes()
    {
        var act = () => CreateOtp(TenMinutes.Add(TimeSpan.FromSeconds(1)));

        act.Should()
            .Throw<ArgumentOutOfRangeException>("NFR-004: el OTP DEBE expirar en <= 10 min")
            .WithParameterName("lifetime");
    }

    [Fact]
    public void Otp_IsUsable_BeforeExpiry()
    {
        CreateOtp().IsUsable(IssuedAt.AddMinutes(9)).Should().BeTrue();
    }

    [Fact]
    public void Otp_IsNotUsable_AtExpiry()
    {
        CreateOtp().IsUsable(IssuedAt.Add(TenMinutes)).Should().BeFalse();
    }

    [Fact]
    public void Consume_MarksTheInstant()
    {
        var otp = CreateOtp();
        var usedAt = IssuedAt.AddMinutes(2);

        otp.Consume(usedAt);

        otp.ConsumedAt.Should().Be(usedAt);
    }

    [Fact]
    public void ConsumedOtp_IsNoLongerUsable()
    {
        var otp = CreateOtp();
        otp.Consume(IssuedAt.AddMinutes(2));

        otp.IsUsable(IssuedAt.AddMinutes(3))
            .Should()
            .BeFalse("un OTP es de un solo uso: se marca consumido, no se borra");
    }

    [Fact]
    public void Consume_Twice_IsRejected()
    {
        var otp = CreateOtp();
        otp.Consume(IssuedAt.AddMinutes(2));

        var act = () => otp.Consume(IssuedAt.AddMinutes(3));

        act.Should().Throw<OtpNotUsableException>();
    }

    [Fact]
    public void Consume_AfterExpiry_IsRejected()
    {
        var otp = CreateOtp();

        var act = () => otp.Consume(IssuedAt.Add(TenMinutes));

        act.Should().Throw<OtpNotUsableException>();
    }

    [Fact]
    public void OtpNotUsableException_CarriesTheContractErrorCode()
    {
        var otp = CreateOtp();

        var exception = Assert.Throws<OtpNotUsableException>(() => otp.Consume(IssuedAt.Add(TenMinutes)));

        exception.ErrorCode
            .Should()
            .Be(
                "AUTH_OTP_REJECTED",
                "ADR-002: un único errorCode para OTP errado y vencido, para no filtrar si el OTP existía");
    }
}
