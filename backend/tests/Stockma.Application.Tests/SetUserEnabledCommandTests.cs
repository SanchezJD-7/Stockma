using FluentAssertions;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class SetUserEnabledCommandTests
{
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeRefreshTokens refreshTokens = new();
    private readonly FakeTrustedDevices trustedDevices = new();

    private SetUserEnabledCommandHandler CreateHandler() =>
        new(accounts, refreshTokens, trustedDevices, TimeProvider.System);

    [Fact]
    public async Task Disable_AMember_RevokesEveryDeviceAndSessionBeforeLockingTheAccount()
    {
        accounts.CurrentRole = TenantRoles.Member;

        var result = await CreateHandler().Handle(new SetUserEnabledCommand(UserId, Enabled: false), default);

        result.UserId.Should().Be(UserId);
        result.Enabled.Should().BeFalse();
        trustedDevices.Revocations.Should()
            .ContainSingle()
            .Which.UserId.Should()
            .Be(UserId, "deshabilitar sin revocar dispositivos es un offboarding a medias");
        refreshTokens.UserRevocations.Should()
            .ContainSingle()
            .Which.UserId.Should()
            .Be(UserId, "las familias de refresh mueren en el mismo acto");
        accounts.AccountEnabledSet.Should()
            .Be((UserId, false),
                "el bloqueo se escribe DESPUÉS de las revocaciones (fail-closed, misma regla que T061)");
    }

    [Fact]
    public async Task Disable_TheLastAdmin_IsRejectedWithoutTouchingAnything()
    {
        accounts.CurrentRole = TenantRoles.TenantAdmin;
        accounts.OtherTenantAdmins = 0;

        Func<Task> act = () => CreateHandler().Handle(new SetUserEnabledCommand(UserId, Enabled: false), default);

        await act.Should()
            .ThrowAsync<ValidationFailedException>()
            .Where(exception => exception.Message.Contains("último TenantAdmin"));
        accounts.AccountEnabledSet.Should().BeNull("ni el lockout ni las revocaciones se tocan si la regla rechaza");
        trustedDevices.Revocations.Should().BeEmpty();
        refreshTokens.UserRevocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Disable_AnAdminWhenAnotherAdminExists_IsAllowed()
    {
        accounts.CurrentRole = TenantRoles.TenantAdmin;
        accounts.OtherTenantAdmins = 1;

        var result = await CreateHandler().Handle(new SetUserEnabledCommand(UserId, Enabled: false), default);

        result.Enabled.Should().BeFalse();
        accounts.AccountEnabledSet.Should().Be((UserId, false));
        refreshTokens.UserRevocations.Should().ContainSingle();
        trustedDevices.Revocations.Should().ContainSingle();
    }

    [Fact]
    public async Task Enable_RestoresAccessWithoutRevokingAnything()
    {
        accounts.CurrentRole = TenantRoles.Member;

        var result = await CreateHandler().Handle(new SetUserEnabledCommand(UserId, Enabled: true), default);

        result.Enabled.Should().BeTrue();
        accounts.AccountEnabledSet.Should().Be((UserId, true));
        refreshTokens.UserRevocations.Should()
            .BeEmpty("rehabilitar no toca sesiones: lo viejo ya murió en el disable, nada resucita");
        trustedDevices.Revocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Disable_ForAUserOfAnotherTenant_ThrowsNotFoundWithoutSideEffects()
    {
        accounts.UserExists = false;
        accounts.CurrentRole = TenantRoles.Member;

        Func<Task> act = () => CreateHandler().Handle(new SetUserEnabledCommand(UserId, Enabled: false), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
        accounts.AccountEnabledSet.Should().BeNull();
        trustedDevices.Revocations.Should().BeEmpty();
        refreshTokens.UserRevocations.Should().BeEmpty();
    }
}
