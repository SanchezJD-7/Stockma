using FluentAssertions;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class ConfirmDeviceCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string Email = "ana@droga.co";
    private const string DeviceId = "device-mostrador";
    private const string Fingerprint = "fp-mostrador";
    private const string Otp = "483920";

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeTrustedDevices devices = new();
    private readonly FakeDeviceOtpService otps = new();
    private readonly FakeJwtTokenService tokens = new();
    private readonly FakeRefreshTokenService sessions = new();
    private readonly FakeTenantContext tenantContext = new(Guid.Empty);

    private ConfirmDeviceCommandHandler CreateHandler() => new(accounts, devices, otps, tokens, sessions, tenantContext);

    private static ConfirmDeviceCommand Command() => new(Email, DeviceId, Fingerprint, Otp);

    private void ArrangeKnownUser() =>
        accounts.ByEmailResult = new LoginIdentity(UserId, TenantId, [TenantRoles.Member]);

    [Fact]
    public async Task Confirm_WithAValidOtp_IssuesTheToken()
    {
        ArrangeKnownUser();

        var result = await CreateHandler().Handle(Command(), default);

        result.AccessToken.Should().Be($"jwt-para-{UserId}");
        result.ExpiresIn.Should().Be(900);
        result.RefreshToken.Should().Be(FakeRefreshTokenService.IssuedToken);
    }

    [Fact]
    public async Task Confirm_WithAValidOtp_OpensANewRefreshTokenFamily()
    {
        ArrangeKnownUser();

        var result = await CreateHandler().Handle(Command(), default);

        result.RefreshToken.Should().Be(FakeRefreshTokenService.IssuedToken);
        sessions.Issued.Should().ContainSingle().Which.Should().Be((TenantId, UserId, DeviceId));
    }

    [Fact]
    public async Task Confirm_ConsumesTheOtpForThatDevice()
    {
        ArrangeKnownUser();

        await CreateHandler().Handle(Command(), default);

        otps.Consumed.Should().ContainSingle().Which.Should().Be((UserId, DeviceId, Otp));
    }

    [Fact]
    public async Task Confirm_MarksTheDeviceAsTrusted()
    {
        ArrangeKnownUser();

        var result = await CreateHandler().Handle(Command(), default);

        devices.TrustAttempts.Should().ContainSingle().Which.Should().Be(DeviceId);
        result.DeviceTrusted.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WithNoTrustSlotLeft_StillIssuesTheToken()
    {
        ArrangeKnownUser();
        devices.TrustGranted = false;

        var result = await CreateHandler().Handle(Command(), default);

        result.AccessToken
            .Should()
            .NotBeNull("FR-007: el acceso NO DEBE bloquearse nunca por el límite de dispositivos");
        result.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Confirm_WithNoTrustSlotLeft_ReportsTheDeviceAsNotTrusted()
    {
        ArrangeKnownUser();
        devices.TrustGranted = false;

        var result = await CreateHandler().Handle(Command(), default);

        result.DeviceTrusted
            .Should()
            .BeFalse("el cliente necesita saber que va a pedir el código en cada ingreso");
    }

    [Fact]
    public async Task Confirm_WithAnInvalidOtp_IsRejected()
    {
        ArrangeKnownUser();
        otps.ConsumeThrows = new OtpNotUsableException();

        Func<Task> act = () => CreateHandler().Handle(Command(), default);

        await act.Should().ThrowAsync<OtpNotUsableException>();
    }

    [Fact]
    public async Task Confirm_WithAnInvalidOtp_DoesNotIssueATokenNorTrustTheDevice()
    {
        ArrangeKnownUser();
        otps.ConsumeThrows = new OtpNotUsableException();

        await Assert.ThrowsAsync<OtpNotUsableException>(() => CreateHandler().Handle(Command(), default));

        tokens.Requests.Should().BeEmpty();
        devices.TrustAttempts.Should().BeEmpty("primero se supera el OTP, después se confía el dispositivo");
    }

    [Fact]
    public async Task Confirm_ForAnUnknownEmail_IsRejectedWithTheSingleOtpError()
    {
        accounts.ByEmailResult = null;

        var exception = await Assert.ThrowsAsync<OtpNotUsableException>(
            () => CreateHandler().Handle(Command(), default));

        exception.ErrorCode
            .Should()
            .Be(
                "AUTH_OTP_REJECTED",
                "ADR-016: email inexistente y OTP errado dan la MISMA respuesta; distinguirlos enumera correos");
    }

    [Fact]
    public async Task Confirm_ForAnUnknownEmail_NeverTouchesTheOtp()
    {
        accounts.ByEmailResult = null;

        await Assert.ThrowsAsync<OtpNotUsableException>(
            () => CreateHandler().Handle(Command(), default));

        otps.Consumed.Should().BeEmpty();
        tenantContext.SetCalls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dev-1")]
    public async Task Confirm_WithALowEntropyDeviceId_IsAValidationFailure(string deviceId)
    {
        ArrangeKnownUser();

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => CreateHandler().Handle(new ConfirmDeviceCommand(Email, deviceId, Fingerprint, Otp), default));

        otps.Consumed.Should().BeEmpty("un deviceId inválido se rechaza antes de gastar el OTP");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Confirm_WithoutAFingerprint_IsRejectedBeforeConsumingTheOtp(string fingerprint)
    {
        ArrangeKnownUser();

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => CreateHandler().Handle(new ConfirmDeviceCommand(Email, DeviceId, fingerprint, Otp), default));

        otps.Consumed.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirm_WithAFingerprintLongerThanTheColumn_IsRejectedBeforeLookingUpTheUser()
    {
        ArrangeKnownUser();
        var fingerprint = new string('f', 257);

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => CreateHandler().Handle(new ConfirmDeviceCommand(Email, DeviceId, fingerprint, Otp), default));

        otps.Consumed.Should().BeEmpty("una huella que no entra en la columna no puede gastar el OTP");
        tenantContext.SetCalls.Should().BeEmpty("la validación de la entrada va antes de mirar el email, igual para cualquier cuenta");
    }

    [Fact]
    public async Task Confirm_WithAFingerprintOfExactly256Characters_IsAccepted()
    {
        ArrangeKnownUser();

        var result = await CreateHandler().Handle(
            new ConfirmDeviceCommand(Email, DeviceId, new string('f', 256), Otp),
            default);

        result.DeviceTrusted.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WhenTrustingTheDeviceFails_DoesNotCommitTheOtpConsumption()
    {
        ArrangeKnownUser();
        devices.TrustThrows = new InvalidOperationException("la base se cayó");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().Handle(Command(), default));

        otps.Committed.Should().BeEmpty("ADR-017: sin JWT no se gasta el OTP; el usuario puede reintentar con el mismo código");
    }

    [Fact]
    public async Task Confirm_WhenTheTokenCannotBeIssued_DoesNotCommitTheOtpConsumption()
    {
        ArrangeKnownUser();
        tokens.Throws = new InvalidOperationException("clave de firma inválida");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().Handle(Command(), default));

        otps.Committed.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirm_WithAValidOtp_CommitsTheConsumptionAfterIssuingTheToken()
    {
        ArrangeKnownUser();

        await CreateHandler().Handle(Command(), default);

        otps.Committed.Should().ContainSingle().Which.Should().Be((UserId, DeviceId, Otp));
    }

    [Fact]
    public async Task Confirm_NormalizesTheDeviceIdBeforeUsingIt()
    {
        ArrangeKnownUser();

        await CreateHandler().Handle(new ConfirmDeviceCommand(Email, $"  {DeviceId}  ", Fingerprint, Otp), default);

        otps.Consumed.Should().ContainSingle().Which.DeviceId.Should().Be(DeviceId);
        devices.TrustAttempts.Should().ContainSingle().Which.Should().Be(DeviceId);
    }

    [Fact]
    public async Task Confirm_PutsTheResolvedTenantInTheToken()
    {
        ArrangeKnownUser();

        await CreateHandler().Handle(Command(), default);

        tokens.Requests[0].TenantId.Should().Be(TenantId);
    }
}
