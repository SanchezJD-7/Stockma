using FluentAssertions;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class BootstrapAdminCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string Email = "admin@droga.co";
    private const string Password = "S3gura#2026";
    private const string PhoneNumber = "+573001234567";

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeTenantAccounts tenants = new();
    private readonly FakeTenantContext tenantContext = new(Guid.NewGuid());

    private BootstrapAdminCommandHandler CreateHandler() => new(tenants, accounts, tenantContext);

    private static BootstrapAdminCommand Command() => new(TenantId, Email, Password, PhoneNumber);

    [Fact]
    public async Task Bootstrap_WithAnUnknownTenant_IsRejectedAndCreatesNoUser()
    {
        tenants.Exists = false;

        Func<Task> act = () => CreateHandler().Handle(Command(), default);

        var exception = await act.Should().ThrowAsync<TenantNotFoundException>();
        exception.Which.ErrorCode.Should().Be("TENANT_NOT_FOUND");
        accounts.Created.Should().BeNull();
    }

    [Fact]
    public async Task Bootstrap_ChecksAndCreatesWhileHoldingTheTargetTenantsLock()
    {
        tenants.Exists = true;
        accounts.LockProbe = () => tenants.HoldingLockFor == TenantId;

        await CreateHandler().Handle(Command(), default);

        accounts.CheckedWhileLocked.Should().BeTrue("ADR-017: sin el lock, dos bootstraps en carrera ven 'sin usuarios' los dos");
        accounts.CreatedWhileLocked.Should().BeTrue();
    }

    [Fact]
    public async Task Bootstrap_WhenTheTenantAlreadyHasUsers_IsRejectedAndCreatesNoUser()
    {
        tenants.Exists = true;
        accounts.HasAnyUser = true;

        Func<Task> act = () => CreateHandler().Handle(Command(), default);

        var exception = await act.Should().ThrowAsync<TenantAlreadyBootstrappedException>();
        exception.Which.ErrorCode.Should().Be("TENANT_ALREADY_BOOTSTRAPPED");
        accounts.Created.Should().BeNull();
    }

    [Fact]
    public async Task Bootstrap_ForAnEmptyExistingTenant_CreatesATenantAdmin()
    {
        tenants.Exists = true;

        var result = await CreateHandler().Handle(Command(), default);

        accounts.Created.Should().NotBeNull();
        accounts.Created!.TenantId.Should().Be(TenantId);
        accounts.Created!.Role.Should().Be(TenantRoles.TenantAdmin);
        accounts.Created!.PhoneNumber.Should().Be(PhoneNumber);
        result.UserId.Should().Be(accounts.CreatedId);
    }

    [Fact]
    public async Task Bootstrap_MovesTheAmbientTenantContextToTheTargetTenantBeforeCreating()
    {
        tenants.Exists = true;

        await CreateHandler().Handle(Command(), default);

        tenantContext.SetCalls.Should().ContainSingle().Which.Should().Be(TenantId);
    }
}
