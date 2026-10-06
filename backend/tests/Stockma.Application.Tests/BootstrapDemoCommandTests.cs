using FluentAssertions;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class BootstrapDemoCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ForeignUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private const string Email = "demo@stockma.app";
    private const string Password = "Demo#Stockma2026";
    private const string PhoneNumber = "+10000000000";

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeTenantAccounts tenants = new();
    private readonly FakeTenantContext tenantContext = new(Guid.Empty);

    private BootstrapDemoCommandHandler CreateHandler() => new(tenants, accounts, tenantContext);

    private static BootstrapDemoCommand Command() => new(TenantId, Email, Password, PhoneNumber);

    [Fact]
    public async Task Bootstrap_TurnsOffTheSecondFactorAndCreatesAMember()
    {
        await CreateHandler().Handle(Command(), default);

        tenants.ProvisionedFor.Should().Be(TenantId);
        tenants.ProvisionedRequireSecondFactor
            .Should()
            .BeFalse("T091: sin el flag apagado un visitante del demo nunca entra, no recibe el SMS");
        accounts.Created.Should().NotBeNull();
        accounts.Created!.TenantId.Should().Be(TenantId);
        accounts.Created!.Email.Should().Be(Email);
        accounts.Created!.PhoneNumber.Should().Be(PhoneNumber);
        accounts.Created!.Role.Should().Be(TenantRoles.Member, "el demo prueba, no administra");
    }

    [Fact]
    public async Task Bootstrap_RunTwice_CreatesTheUserOnlyOnce()
    {
        var first = await CreateHandler().Handle(Command(), default);

        accounts.ByEmailResult = new LoginIdentity(first.UserId, TenantId, [TenantRoles.Member]);
        var second = await CreateHandler().Handle(Command(), default);

        accounts.CreateCalls
            .Should()
            .Be(1, "el seed es idempotente: re-correrlo no duplica el usuario ni falla");
        second.UserId.Should().Be(first.UserId);
        tenants.ProvisionCalls
            .Should()
            .Be(2, "el tenant sí se reasegura en cada corrida, por si alguien apagó el flag a mano");
    }

    [Fact]
    public async Task Bootstrap_WhenTheEmailBelongsToAnotherTenant_IsRejected()
    {
        accounts.ByEmailResult = new LoginIdentity(ForeignUserId, Guid.NewGuid(), [TenantRoles.Member]);

        Func<Task> act = () => CreateHandler().Handle(Command(), default);

        var exception = await act.Should().ThrowAsync<EmailAlreadyRegisteredException>();
        exception.Which.ErrorCode.Should().Be("AUTH_EMAIL_DUPLICATE");
        accounts.CreateCalls.Should().Be(0);
    }

    [Fact]
    public async Task Bootstrap_MovesTheAmbientTenantContextToTheDemoTenantBeforeCreating()
    {
        await CreateHandler().Handle(Command(), default);

        tenantContext.SetCalls.Should().ContainSingle().Which.Should().Be(TenantId);
    }
}
