using FluentAssertions;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class SetUserRoleCommandTests
{
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeRefreshTokens refreshTokens = new();
    private readonly FakeTrustedDevices trustedDevices = new();

    private SetUserRoleCommandHandler CreateHandler() =>
        new(accounts, refreshTokens, trustedDevices, TimeProvider.System);

    [Fact]
    public async Task SetRole_PromotesAMemberWithoutTouchingSessions()
    {
        accounts.CurrentRole = TenantRoles.Member;

        var result = await CreateHandler().Handle(new SetUserRoleCommand(UserId, TenantRoles.TenantAdmin), default);

        result.Role.Should().Be(TenantRoles.TenantAdmin);
        accounts.RoleSet.Should().Be((UserId, TenantRoles.TenantAdmin));
        refreshTokens.UserRevocations.Should()
            .BeEmpty("dar poder no expulsa a nadie: el rol nuevo llega con el próximo refresh");
        trustedDevices.Revocations.Should().BeEmpty();
    }

    [Fact]
    public async Task SetRole_DemotingAnAdminWithOtherAdminsRevokesEverything()
    {
        accounts.CurrentRole = TenantRoles.TenantAdmin;
        accounts.OtherTenantAdmins = 1;

        var result = await CreateHandler().Handle(new SetUserRoleCommand(UserId, TenantRoles.Member), default);

        result.Role.Should().Be(TenantRoles.Member);
        accounts.RoleSet.Should().Be((UserId, TenantRoles.Member));
        refreshTokens.UserRevocations.Should().ContainSingle()
            .Which.UserId.Should()
            .Be(UserId, "quitar poder obliga a re-login con claims frescos");
        trustedDevices.Revocations.Should().ContainSingle();
    }

    [Fact]
    public async Task SetRole_DemotingTheLastAdminIsRejectedWithoutSideEffects()
    {
        accounts.CurrentRole = TenantRoles.TenantAdmin;
        accounts.OtherTenantAdmins = 0;

        Func<Task> act = () => CreateHandler().Handle(new SetUserRoleCommand(UserId, TenantRoles.Member), default);

        await act.Should()
            .ThrowAsync<ValidationFailedException>()
            .Where(exception => exception.Message.Contains("último TenantAdmin"));
        accounts.RoleSet.Should().BeNull();
        refreshTokens.UserRevocations.Should().BeEmpty("la validación va antes de cualquier efecto");
        trustedDevices.Revocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("")]
    [InlineData(null)]
    public async Task SetRole_WithAnUnknownRole_TouchesNothing(string? role)
    {
        accounts.CurrentRole = TenantRoles.TenantAdmin;

        Func<Task> act = () => CreateHandler().Handle(new SetUserRoleCommand(UserId, role!), default);

        await act.Should().ThrowAsync<ValidationFailedException>();
        accounts.RoleSet.Should().BeNull();
        refreshTokens.UserRevocations.Should().BeEmpty();
        trustedDevices.Revocations.Should().BeEmpty();
    }

    [Fact]
    public async Task SetRole_ForAnUnknownUser_ReturnsNotFoundWithoutSideEffects()
    {
        accounts.RoleUserExists = false;

        Func<Task> act = () => CreateHandler().Handle(new SetUserRoleCommand(UserId, TenantRoles.TenantAdmin), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
        accounts.RoleSet.Should().BeNull();
        refreshTokens.UserRevocations.Should().BeEmpty();
    }

    [Fact]
    public async Task SetRole_WithTheSameRoleIsIdempotent()
    {
        accounts.CurrentRole = TenantRoles.Member;

        var result = await CreateHandler().Handle(new SetUserRoleCommand(UserId, TenantRoles.Member), default);

        result.Role.Should().Be(TenantRoles.Member);
        accounts.RoleSet.Should().BeNull("no hay nada que cambiar");
        refreshTokens.UserRevocations.Should().BeEmpty();
    }
}
