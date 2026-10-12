using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;
using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Tests;

public class PhoneNumberChangeCommandTests
{
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private const string CurrentPhone = "+573001234567";
    private const string NewPhone = "+573009876543";

    private readonly FakeUserAccounts accounts = new() { CurrentPhoneNumber = CurrentPhone };
    private readonly FakeDeviceOtpService deviceOtps = new();
    private readonly FakeTrustedDevices trustedDevices = new();
    private readonly FakeRefreshTokens refreshTokens = new();
    private readonly FakeSmsSender smsSender = new();
    private readonly FakeTenantSettingsProvider tenantSettings =
        new(new FakeTenantContext(Guid.NewGuid()));

    private RequestPhoneNumberChangeCommandHandler CreateRequestHandler() =>
        new(accounts, deviceOtps, tenantSettings);

    private ConfirmPhoneNumberChangeCommandHandler CreateConfirmHandler() =>
        new(
            accounts,
            deviceOtps,
            refreshTokens,
            trustedDevices,
            smsSender,
            TimeProvider.System,
            NullLogger<ConfirmPhoneNumberChangeCommandHandler>.Instance);

    [Fact]
    public async Task RequestChange_IssuesTheOtpAndReportsTheCurrentNumberMasked()
    {
        var handler = CreateRequestHandler();

        var result = await handler.Handle(new RequestPhoneNumberChangeCommand(UserId, NewPhone), default);

        deviceOtps.PhoneChangeIssued.Should().ContainSingle().Which.Should().Be((UserId, NewPhone));
        result.OtpSentToMasked.Should().Be(PhoneNumber.Mask(CurrentPhone), "el front muestra a QUÉ número fue el código");
        result.ExpiresInSeconds.Should().Be(600);
    }

    [Theory]
    [InlineData("3001234567")]
    [InlineData("+54 11 2345 6789")]
    [InlineData("")]
    public async Task RequestChange_WithAnInvalidE164_IssuesNothing(string newPhone)
    {
        var handler = CreateRequestHandler();

        Func<Task> act = () => handler.Handle(new RequestPhoneNumberChangeCommand(UserId, newPhone), default);

        await act.Should().ThrowAsync<ValidationFailedException>();
        deviceOtps.PhoneChangeIssued.Should().BeEmpty("un número inválido no llega a emitir OTP");
    }

    [Fact]
    public async Task RequestChange_WithoutAnEnrolledNumber_ReturnsNotEnrolled()
    {
        accounts.CurrentPhoneNumber = null;
        var handler = CreateRequestHandler();

        Func<Task> act = () => handler.Handle(new RequestPhoneNumberChangeCommand(UserId, NewPhone), default);

        await act.Should().ThrowAsync<PhoneNotEnrolledException>();
        deviceOtps.PhoneChangeIssued.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestChange_ToTheCurrentNumber_IssuesNothing()
    {
        var handler = CreateRequestHandler();

        Func<Task> act = () => handler.Handle(new RequestPhoneNumberChangeCommand(UserId, CurrentPhone), default);

        await act.Should().ThrowAsync<ValidationFailedException>();
        deviceOtps.PhoneChangeIssued.Should().BeEmpty("no hay nada que cambiar");
    }

    [Fact]
    public async Task RequestChange_WhileAnotherChangeIsPending_Throws409AndEmitsNothing()
    {
        deviceOtps.IssueStatus = PhoneChangeIssueStatus.PendingExists;
        var handler = CreateRequestHandler();

        Func<Task> act = () => handler.Handle(new RequestPhoneNumberChangeCommand(UserId, NewPhone), default);

        var error = await act.Should().ThrowAsync<PhoneChangePendingException>();
        error.Which.PendingExpiresInSeconds.Should().Be(540, "el 409 le dice al front cuánto falta para que venza el pendiente");
        deviceOtps.PhoneChangeIssued.Should().BeEmpty("con un cambio en vuelo no se emite otro código");
    }

    [Fact]
    public async Task RequestChange_WithANumberFromAnotherCountry_IssuesNothing()
    {
        var handler = CreateRequestHandler();

        Func<Task> act = () => handler.Handle(new RequestPhoneNumberChangeCommand(UserId, "+525512345678"), default);

        await act.Should().ThrowAsync<ValidationFailedException>(
            "el tenant es de Colombia: un número mexicano no pasa la regla del país");
        deviceOtps.PhoneChangeIssued.Should().BeEmpty("un número de otro país no llega a emitir OTP");
    }

    [Fact]
    public async Task RequestChange_WithAColombianLandline_IssuesNothing()
    {
        var handler = CreateRequestHandler();

        Func<Task> act = () => handler.Handle(new RequestPhoneNumberChangeCommand(UserId, "+5712345678"), default);

        await act.Should().ThrowAsync<ValidationFailedException>(
            "en Colombia sólo se aceptan móviles (10 dígitos que empiezan con 3), no fijos");
        deviceOtps.PhoneChangeIssued.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestChange_WithoutACountryRule_AcceptsAnyE164()
    {
        tenantSettings.CountryCode = null;
        var handler = CreateRequestHandler();

        var result = await handler.Handle(new RequestPhoneNumberChangeCommand(UserId, "+541123456789"), default);

        result.OtpSentToMasked.Should().Be(PhoneNumber.Mask(CurrentPhone), "sin país configurado sólo se exige E.164");
        deviceOtps.PhoneChangeIssued.Should().ContainSingle().Which.Should().Be((UserId, "+541123456789"));
    }

    [Fact]
    public async Task Confirm_AppliesTheNewNumberAndNotifiesTheOldOne()
    {
        deviceOtps.ConsumePhoneNumberChangeReturns = NewPhone;
        var handler = CreateConfirmHandler();

        var result = await handler.Handle(new ConfirmPhoneNumberChangeCommand(UserId, "483920"), default);

        accounts.PhoneNumberSet.Should().Be((UserId, NewPhone));
        result.PhoneNumberMasked.Should().Be(PhoneNumber.Mask(NewPhone));
        result.PhoneNumberConfirmed.Should().BeTrue();

        var notice = smsSender.Sent.Should().ContainSingle().Subject;
        notice.PhoneNumber.Should().Be(CurrentPhone, "el aviso llega al número viejo: es la única pista del titular original");
        notice.Message.Should().Contain("cambiado");
    }

    [Fact]
    public async Task Confirm_RevokesEverySessionAndTrustedDevice()
    {
        deviceOtps.ConsumePhoneNumberChangeReturns = NewPhone;
        var handler = CreateConfirmHandler();

        await handler.Handle(new ConfirmPhoneNumberChangeCommand(UserId, "483920"), default);

        trustedDevices.Revocations.Should().Contain((UserId, (Guid?)null), "todos los dispositivos confiables del titular pierden la confianza");
        refreshTokens.UserRevocations.Should().ContainSingle().Which.UserId.Should().Be(UserId, "las sesiones abiertas mueren en el acto");
        deviceOtps.Invalidations.Should().ContainSingle().Which.UserId.Should().Be(UserId, "los OTPs restantes del usuario se queman");
        accounts.PhoneNumberSet.Should().Be((UserId, NewPhone));
    }

    [Fact]
    public async Task Confirm_SucceedsEvenIfTheNoticeSmsFails()
    {
        deviceOtps.ConsumePhoneNumberChangeReturns = NewPhone;
        smsSender.Throws = new SmsDeliveryException("Twilio respondió 500");
        var handler = CreateConfirmHandler();

        var result = await handler.Handle(new ConfirmPhoneNumberChangeCommand(UserId, "483920"), default);

        result.PhoneNumberConfirmed.Should().BeTrue("el aviso es best-effort: el cambio ya estaba confirmado con OTP");
        accounts.PhoneNumberSet.Should().Be((UserId, NewPhone));
    }

    [Fact]
    public async Task Confirm_WithoutAnEnrolledNumber_ReturnsNotEnrolled()
    {
        accounts.CurrentPhoneNumber = null;
        var handler = CreateConfirmHandler();

        Func<Task> act = () => handler.Handle(new ConfirmPhoneNumberChangeCommand(UserId, "483920"), default);

        await act.Should().ThrowAsync<PhoneNotEnrolledException>();
        deviceOtps.PhoneChangeConsumed.Should().BeEmpty();
        smsSender.Sent.Should().BeEmpty();
        trustedDevices.Revocations.Should().BeEmpty("sin número no hay nada que revocar");
        refreshTokens.UserRevocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirm_WithARejectedOtp_SendsNoNoticeKeepsTheNumberAndRevokesNothing()
    {
        deviceOtps.ConsumeThrows = new OtpNotUsableException();
        var handler = CreateConfirmHandler();

        Func<Task> act = () => handler.Handle(new ConfirmPhoneNumberChangeCommand(UserId, "000000"), default);

        await act.Should().ThrowAsync<OtpNotUsableException>();
        accounts.PhoneNumberSet.Should().BeNull("sin OTP válido no se escribe nada ni se avisa a nadie");
        smsSender.Sent.Should().BeEmpty();
        trustedDevices.Revocations.Should().BeEmpty("un OTP equivocado no castiga al titular con la pérdida de sesiones");
        refreshTokens.UserRevocations.Should().BeEmpty();
        deviceOtps.Invalidations.Should().BeEmpty();
    }
}
