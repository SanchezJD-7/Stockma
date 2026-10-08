using FluentAssertions;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class SetUserPhoneNumberCommandTests
{
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string ValidPhone = "+573001234567";

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeRefreshTokens refreshTokens = new();
    private readonly FakeTrustedDevices trustedDevices = new();
    private readonly FakeDeviceOtpService deviceOtps = new();

    private SetUserPhoneNumberCommandHandler CreateHandler() =>
        new(accounts, refreshTokens, trustedDevices, deviceOtps, TimeProvider.System);

    [Fact]
    public async Task SetPhoneNumber_PersistsTheNumberAndMasksItInTheResponse()
    {
        var result = await CreateHandler().Handle(new SetUserPhoneNumberCommand(UserId, ValidPhone), default);

        accounts.PhoneNumberSet.Should().Be((UserId, ValidPhone));
        result.UserId.Should().Be(UserId);
        result.PhoneNumberMasked.Should().Be("+57300*****67");
        result.PhoneNumberConfirmed.Should().BeTrue("el admin atestigua el número que acaba de cargar");
    }

    [Fact]
    public async Task SetPhoneNumber_RevokesEverySessionOfTheUser()
    {
        await CreateHandler().Handle(new SetUserPhoneNumberCommand(UserId, ValidPhone), default);

        refreshTokens.UserRevocations.Should().ContainSingle()
            .Which.UserId.Should()
            .Be(UserId, "T050: cambiar el número sin cortar la sesión deja el celular robado operando");
    }

    [Fact]
    public async Task SetPhoneNumber_RevokesEveryTrustedDeviceOfTheUser()
    {
        await CreateHandler().Handle(new SetUserPhoneNumberCommand(UserId, ValidPhone), default);

        trustedDevices.Revocations.Should().ContainSingle();
        trustedDevices.Revocations[0].UserId.Should().Be(UserId);
        trustedDevices.Revocations[0].DeviceId.Should()
            .BeNull("deviceId = null significa TODOS los de ese usuario: un dispositivo confiable saltea el OTP");
    }

    [Fact]
    public async Task SetPhoneNumber_InvalidatesTheOtpsInFlight()
    {
        await CreateHandler().Handle(new SetUserPhoneNumberCommand(UserId, ValidPhone), default);

        deviceOtps.Invalidations.Should().ContainSingle()
            .Which.UserId.Should()
            .Be(UserId, "un OTP enviado al número viejo (celular robado) no puede seguir sirviendo");
    }

    [Theory]
    [InlineData("3001234567")]
    [InlineData("")]
    [InlineData("no es un numero")]
    public async Task SetPhoneNumber_WithAnInvalidNumber_TouchesNothing(string phoneNumber)
    {
        Func<Task> act = () => CreateHandler().Handle(new SetUserPhoneNumberCommand(UserId, phoneNumber), default);

        await act.Should().ThrowAsync<ValidationFailedException>();

        trustedDevices.Revocations.Should().BeEmpty("la validación va antes de cualquier efecto secundario");
        refreshTokens.UserRevocations.Should().BeEmpty();
        deviceOtps.Invalidations.Should().BeEmpty();
        accounts.PhoneNumberSet.Should().BeNull();
    }
}
