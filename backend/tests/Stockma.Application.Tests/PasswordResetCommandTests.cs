using FluentAssertions;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;
public class PasswordResetCommandTests
{
    private static readonly Guid UserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeDeviceOtpService deviceOtps = new();
    private readonly FakeRefreshTokens refreshTokens = new();
    private readonly FakeTenantContext tenantContext = new(Guid.Empty);

    private RequestPasswordResetCommandHandler CreateRequestHandler() =>
        new(accounts, deviceOtps, tenantContext);

    private ResetPasswordCommandHandler CreateResetHandler() =>
        new(accounts, deviceOtps, refreshTokens, tenantContext, TimeProvider.System);

    private ResetUserPasswordCommandHandler CreateAdminResetHandler() =>
        new(accounts, deviceOtps, refreshTokens, TimeProvider.System);

    private LoginIdentity Identity() => new(UserId, TenantId, [TenantRoles.Member]);

    [Fact]
    public async Task Request_ForAnUnknownEmail_IssuesNothingAndSetsNoTenant()
    {
        accounts.ByEmailResult = null;

        await CreateRequestHandler().Handle(new RequestPasswordResetCommand("nadie@x.com"), default);

        deviceOtps.PasswordResetIssued.Should().BeEmpty("la respuesta es 202 igual, pero no se emite código");
        tenantContext.SetCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_ForAKnownEmail_SetsTheTenantBeforeIssuingAndSendsTheCode()
    {
        accounts.ByEmailResult = Identity();

        var issuedOrder = new List<string>();
        tenantContext.OnSet = () => issuedOrder.Add("tenant");
        deviceOtps.OnIssuePasswordReset = () => issuedOrder.Add("issue");

        await CreateRequestHandler().Handle(new RequestPasswordResetCommand("dueño@x.com"), default);

        deviceOtps.PasswordResetIssued.Should().ContainSingle().Which.Should().Be(UserId);
        issuedOrder.Should().Equal(
            new[] { "tenant", "issue" },
            "emitir sin tenant resuelto rompe RLS: el código caería con tenant vacío");
    }

    [Fact]
    public async Task Request_WithoutAnEnrolledPhone_StillAnswersUniformlyWithoutThrowing()
    {
        accounts.ByEmailResult = Identity();
        deviceOtps.IssueThrows = new PhoneNotEnrolledException();

        Func<Task> act = () => CreateRequestHandler().Handle(new RequestPasswordResetCommand("sin@celular.com"), default);

        await act.Should().NotThrowAsync("el caso sin número es del admin: misma respuesta uniforme, sin 403");
        deviceOtps.PasswordResetIssued.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_WhenTheUserWasDeletedConcurrently_StillAnswersUniformly()
    {
        accounts.ByEmailResult = Identity();
        deviceOtps.IssueThrows = new UserNotFoundException(UserId);

        Func<Task> act = () => CreateRequestHandler().Handle(new RequestPasswordResetCommand("borrado@x.com"), default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Reset_ForAnUnknownEmail_ThrowsTheUniformOtpRejectionWithoutConsuming()
    {
        accounts.ByEmailResult = null;

        Func<Task> act = () => CreateResetHandler()
            .Handle(new ResetPasswordCommand("nadie@x.com", "123456", "Nueva-Larga-1"), default);

        await act.Should()
            .ThrowAsync<OtpNotUsableException>()
            .Where(exception => exception.ErrorCode == "AUTH_OTP_REJECTED",
                "email inexistente debe responder exactamente igual que código erróneo");
        deviceOtps.PasswordResetConsumed.Should().BeEmpty();
    }

    [Fact]
    public async Task Reset_WithAValidCode_SetsThePasswordRevokesEverySessionAndKillsTheRemainingOtps()
    {
        accounts.ByEmailResult = Identity();

        await CreateResetHandler()
            .Handle(new ResetPasswordCommand("dueño@x.com", "483920", "Nueva-Larga-1"), default);

        deviceOtps.PasswordResetConsumed.Should()
            .ContainSingle()
            .Which.UserId.Should().Be(UserId);
        accounts.PasswordSet.Should().Be((UserId, "Nueva-Larga-1"));
        refreshTokens.UserRevocations.Should()
            .ContainSingle()
            .Which.UserId.Should()
            .Be(UserId, "un reset que no corta sesiones no echa al atacante que ya tenía sesión (T065)");
        deviceOtps.Invalidations.Should()
            .ContainSingle()
            .Which.UserId.Should()
            .Be(UserId, "los OTPs vivos emitidos bajo la contraseña vieja mueren junto con ella");
    }

    [Fact]
    public async Task Reset_WhenTheCodeIsRejected_PropagatesAndChangesNothing()
    {
        accounts.ByEmailResult = Identity();
        deviceOtps.ConsumeThrows = new OtpNotUsableException();

        Func<Task> act = () => CreateResetHandler()
            .Handle(new ResetPasswordCommand("dueño@x.com", "000000", "Nueva-Larga-1"), default);

        await act.Should().ThrowAsync<OtpNotUsableException>();
        accounts.PasswordSet.Should().BeNull("sin consume no hay cambio de contraseña");
        refreshTokens.UserRevocations.Should().BeEmpty();
        deviceOtps.Invalidations.Should().BeEmpty();
    }

    [Fact]
    public async Task Reset_WhenTheNewPasswordFailsPolicy_NothingElseHappens()
    {
        accounts.ByEmailResult = Identity();
        accounts.SetPasswordThrows = new ValidationFailedException("La nueva contraseña no cumple las reglas.");

        Func<Task> act = () => CreateResetHandler()
            .Handle(new ResetPasswordCommand("dueño@x.com", "483920", "123"), default);

        await act.Should().ThrowAsync<ValidationFailedException>();
        refreshTokens.UserRevocations.Should()
            .BeEmpty("la política se evalúa ANTES de cortar nada: un error 400 no debe costarle las sesiones al usuario");
        deviceOtps.Invalidations.Should().BeEmpty();
    }

    [Fact]
    public async Task AdminReset_ForAnUnknownUser_ThrowsNotFoundWithoutTouchingSessions()
    {
        accounts.UserExists = false;

        Func<Task> act = () => CreateAdminResetHandler()
            .Handle(new ResetUserPasswordCommand(UserId, "Nueva-Larga-1"), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
        refreshTokens.UserRevocations.Should().BeEmpty();
        deviceOtps.Invalidations.Should().BeEmpty();
        accounts.PasswordSet.Should().BeNull();
    }

    [Fact]
    public async Task AdminReset_Succeeds_RevokesSessionsInvalidatesOtpsAndSetsThePassword()
    {
        accounts.CurrentRole = TenantRoles.Member;

        var result = await CreateAdminResetHandler()
            .Handle(new ResetUserPasswordCommand(UserId, "Nueva-Larga-1"), default);

        result.UserId.Should().Be(UserId);
        refreshTokens.UserRevocations.Should().ContainSingle();
        deviceOtps.Invalidations.Should().ContainSingle();
        accounts.PasswordSet.Should().Be((UserId, "Nueva-Larga-1"));
    }

    [Fact]
    public async Task AdminReset_WhenThePasswordFailsPolicy_TheSessionsStayCut()
    {
        accounts.CurrentRole = TenantRoles.Member;
        accounts.SetPasswordThrows = new ValidationFailedException("La nueva contraseña no cumple las reglas.");

        Func<Task> act = () => CreateAdminResetHandler()
            .Handle(new ResetUserPasswordCommand(UserId, "123"), default);

        await act.Should().ThrowAsync<ValidationFailedException>();
        refreshTokens.UserRevocations.Should()
            .ContainSingle("fail-closed a la T061/T068: primero se corta el acceso, aunque la contraseña después falle");
        accounts.PasswordSet.Should().BeNull();
    }
}
