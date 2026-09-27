using FluentAssertions;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class LoginCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string Email = "ana@droga.co";
    private const string Password = "S3gura#2026";
    private const string DeviceId = "device-mostrador";

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeTrustedDevices devices = new();
    private readonly FakeDeviceOtpService otps = new();
    private readonly FakeJwtTokenService tokens = new();
    private readonly FakeTenantContext tenantContext = new(Guid.Empty);

    private LoginCommandHandler CreateHandler() => new(accounts, devices, otps, tokens, tenantContext);

    private static LoginCommand Command(string? deviceId = DeviceId) =>
        new(Email, Password, deviceId);

    private void ArrangeValidUser(params string[] roles) =>
        accounts.CredentialsResult = new LoginIdentity(UserId, TenantId, roles.Length == 0 ? [TenantRoles.Member] : roles);

    [Fact]
    public async Task Login_FromATrustedDevice_IssuesTheTokenInOneStep()
    {
        ArrangeValidUser();
        devices.Trusted = true;

        var result = await CreateHandler().Handle(Command(), default);

        result.AccessToken.Should().Be($"jwt-para-{UserId}");
        result.ExpiresIn.Should().Be(3600);
        result.RequiresDeviceConfirmation.Should().BeFalse("FR-006: el dispositivo confiable saltea el OTP");
        otps.Issued.Should().BeEmpty("no se manda SMS a un dispositivo ya confiable");
    }

    [Fact]
    public async Task Login_PutsTheResolvedTenantInTheToken()
    {
        ArrangeValidUser();
        devices.Trusted = true;

        await CreateHandler().Handle(Command(), default);

        tokens.Requests.Should().ContainSingle();
        tokens.Requests[0].TenantId
            .Should()
            .Be(TenantId, "ADR-001: el tenant se resuelve desde el email, no desde un header");
    }

    [Fact]
    public async Task Login_PassesTheUsersRolesToTheToken()
    {
        ArrangeValidUser(TenantRoles.TenantAdmin);
        devices.Trusted = true;

        await CreateHandler().Handle(Command(), default);

        tokens.Requests[0].Roles.Should().BeEquivalentTo([TenantRoles.TenantAdmin]);
    }

    [Fact]
    public async Task Login_FromAnUnknownDevice_AsksForConfirmationAndSendsTheOtp()
    {
        ArrangeValidUser();
        devices.Trusted = false;

        var result = await CreateHandler().Handle(Command(), default);

        result.RequiresDeviceConfirmation.Should().BeTrue();
        result.AccessToken.Should().BeNull("FR-008: sin superar el OTP no hay token");
        otps.Issued.Should().ContainSingle().Which.Should().Be((UserId, DeviceId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dev-1")]
    public async Task Login_WithNoDeviceId_IsRejected(string? deviceId)
    {
        ArrangeValidUser();
        devices.Trusted = true;

        Func<Task> act = () => CreateHandler().Handle(Command(deviceId), default);

        await act.Should()
            .ThrowAsync<ValidationFailedException>(
                "el OTP se guarda atado a un deviceId: sin el, no hay donde clavarlo y no se puede "
                + "decidir si el dispositivo es confiable");
    }

    [Fact]
    public async Task Login_WithNoDeviceId_IssuesNothing()
    {
        ArrangeValidUser();

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => CreateHandler().Handle(Command(deviceId: null), default));

        otps.Issued.Should().BeEmpty();
        tokens.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_WithAnUnknownEmail_IsRejected()
    {
        accounts.CredentialsResult = null;

        Func<Task> act = () => CreateHandler().Handle(Command(), default);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_IsRejectedIdentically()
    {
        accounts.CredentialsResult = null;

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateHandler().Handle(Command(), default));

        exception.ErrorCode
            .Should()
            .Be(
                "AUTH_INVALID_CREDENTIALS",
                "T055c: email inexistente, contraseña errada y usuario de otro tenant dan el MISMO error. "
                + "Distinguirlos permite enumerar qué correos usan Stockma");
    }

    [Fact]
    public async Task Login_WhenRejected_DoesNotSendAnySms()
    {
        accounts.CredentialsResult = null;

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateHandler().Handle(Command(), default));

        otps.Issued.Should().BeEmpty("un SMS enviado revelaría que el email existe");
    }

    [Fact]
    public async Task Login_WhenRejected_DoesNotIssueAToken()
    {
        accounts.CredentialsResult = null;

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateHandler().Handle(Command(), default));

        tokens.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_ForAUserWithoutPhoneNumber_PropagatesTheEnrollmentError()
    {
        ArrangeValidUser();
        devices.Trusted = false;
        otps.IssueThrows = new PhoneNotEnrolledException();

        Func<Task> act = () => CreateHandler().Handle(Command(), default);

        await act.Should()
            .ThrowAsync<PhoneNotEnrolledException>(
                "ADR-004: se bloquea el acceso, NO se entra salteando el 2FA");

        tokens.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_NeverTrustsADeviceByItself()
    {
        ArrangeValidUser();
        devices.Trusted = false;

        await CreateHandler().Handle(Command(), default);

        devices.TrustAttempts
            .Should()
            .BeEmpty("confiar un dispositivo es acto de confirm-device, tras superar el OTP");
    }

    [Fact]
    public async Task Login_ActivatesTheResolvedTenantForTheRestOfTheRequest()
    {
        ArrangeValidUser();
        devices.Trusted = false;

        await CreateHandler().Handle(Command(), default);

        tenantContext.SetCalls
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                TenantId,
                "T055b: la excepcion se limita a la busqueda. Resuelto el tenant, todo lo que sigue "
                + "-emitir el OTP, consultar dispositivos- pasa por el filtro normal");
    }

    [Fact]
    public async Task Login_WhenRejected_NeverActivatesATenant()
    {
        accounts.CredentialsResult = null;

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateHandler().Handle(Command(), default));

        tenantContext.SetCalls.Should().BeEmpty();
    }
    [Fact]
    public async Task Login_WithNoDeviceId_IsRejectedBeforeCheckingTheCredentials()
    {
        accounts.CredentialsResult = null;

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(
            () => CreateHandler().Handle(Command(deviceId: "corto"), default));

        exception.ErrorCode.Should().Be(
            "VALIDATION_FAILED",
            "auth-api.md: sin deviceId es 400, y el mismo 400 para cualquier email: no enumera");
    }

    [Fact]
    public async Task Login_NormalizesTheDeviceIdBeforeUsingIt()
    {
        ArrangeValidUser();
        devices.Trusted = false;

        await CreateHandler().Handle(Command($"  {DeviceId}  "), default);

        devices.CheckedDeviceIds.Should().ContainSingle().Which.Should().Be(DeviceId);
        otps.Issued.Should().ContainSingle().Which.DeviceId.Should().Be(DeviceId);
    }
}
