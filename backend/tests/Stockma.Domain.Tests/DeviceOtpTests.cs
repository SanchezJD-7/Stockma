using FluentAssertions;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

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
    [Fact]
    public void NewOtp_HasNoFailedAttempts()
    {
        CreateOtp().FailedAttempts.Should().Be(0);
    }

    [Fact]
    public void RegisterFailedAttempt_CountsTheAttempt()
    {
        var otp = CreateOtp();

        otp.RegisterFailedAttempt(IssuedAt.AddMinutes(1), maxAttempts: 5);

        otp.FailedAttempts.Should().Be(1, "FR-008: el intento fallido DEBE quedar registrado");
        otp.IsUsable(IssuedAt.AddMinutes(1)).Should().BeTrue("un intento por debajo del tope no quema el código");
    }

    [Fact]
    public void RegisterFailedAttempt_ReachingTheMaximum_BurnsTheOtp()
    {
        var otp = CreateOtp();
        var at = IssuedAt.AddMinutes(1);

        foreach (var _ in Enumerable.Range(0, 5))
        {
            otp.RegisterFailedAttempt(at, maxAttempts: 5);
        }

        otp.IsUsable(at).Should().BeFalse("ADR-016: al llegar al tope de intentos el OTP queda quemado");
        otp.InvalidatedAt.Should().Be(at);
        otp.ConsumedAt.Should().BeNull("quemar no es consumir: nadie superó el OTP");
    }

    [Fact]
    public void RegisterFailedAttempt_OnABurnedOtp_IsRejected()
    {
        var otp = CreateOtp();
        var at = IssuedAt.AddMinutes(1);
        otp.RegisterFailedAttempt(at, maxAttempts: 1);

        var act = () => otp.RegisterFailedAttempt(at, maxAttempts: 1);

        act.Should().Throw<OtpNotUsableException>();
        otp.FailedAttempts.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegisterFailedAttempt_RejectsANonPositiveMaximum(int maxAttempts)
    {
        var act = () => CreateOtp().RegisterFailedAttempt(IssuedAt, maxAttempts);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxAttempts");
    }

    [Fact]
    public void Invalidate_MakesTheOtpUnusable()
    {
        var otp = CreateOtp();
        var at = IssuedAt.AddMinutes(1);

        otp.Invalidate(at);

        otp.IsUsable(at).Should().BeFalse("ADR-016: emitir un OTP nuevo invalida los anteriores");
        otp.InvalidatedAt.Should().Be(at);
    }

    [Fact]
    public void Invalidate_AnInvalidatedOtp_KeepsTheFirstInstant()
    {
        var otp = CreateOtp();
        otp.Invalidate(IssuedAt.AddMinutes(1));

        otp.Invalidate(IssuedAt.AddMinutes(2));

        otp.InvalidatedAt.Should().Be(IssuedAt.AddMinutes(1));
    }

    [Fact]
    public void Invalidate_AConsumedOtp_DoesNothing()
    {
        var otp = CreateOtp();
        otp.Consume(IssuedAt.AddMinutes(1));

        otp.Invalidate(IssuedAt.AddMinutes(2));

        otp.InvalidatedAt.Should().BeNull("un OTP ya usado no se reescribe como invalidado");
    }

    [Fact]
    public void Consume_AnInvalidatedOtp_IsRejected()
    {
        var otp = CreateOtp();
        otp.Invalidate(IssuedAt.AddMinutes(1));

        var act = () => otp.Consume(IssuedAt.AddMinutes(2));

        act.Should().Throw<OtpNotUsableException>();
    }

    [Fact]
    public void NewOtp_NormalizesTheDeviceIdentifier()
    {
        var otp = new DeviceOtp(TenantId, UserId, "  device-abc  ", "hash", IssuedAt, TenMinutes);

        otp.DeviceId.Should().Be(DeviceIdentifier.Normalize("  device-abc  "));
    }
}
