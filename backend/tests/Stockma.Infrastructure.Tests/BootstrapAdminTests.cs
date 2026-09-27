using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.DependencyInjection;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Tests;

public class BootstrapAdminTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "S3gura#2026Larga";

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = postgres.AppUserConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private async Task<Guid> SeedEmptyTenantAsync()
    {
        var tenantId = Guid.NewGuid();

        await using var context = new StockmaDbContext(
            new DbContextOptionsBuilder<StockmaDbContext>().UseNpgsql(postgres.ConnectionString).Options,
            new Tenancy.TenantContext());
        await context.Database.ExecuteSqlRawAsync("INSERT INTO tenants (id) VALUES ({0});", tenantId);

        return tenantId;
    }

    private static async Task<RegisteredUser> BootstrapAsync(ServiceProvider provider, Guid tenantId, string email)
    {
        await using var scope = provider.CreateAsyncScope();
        var handler = new BootstrapAdminCommandHandler(
            scope.ServiceProvider.GetRequiredService<ITenantAccounts>(),
            scope.ServiceProvider.GetRequiredService<IUserAccounts>(),
            scope.ServiceProvider.GetRequiredService<ITenantContext>());

        return await handler.Handle(new BootstrapAdminCommand(tenantId, email, Password, "+573001234567"), default);
    }

    private async Task<List<string>> AdminsOfAsync(Guid tenantId)
    {
        await using var context = new StockmaDbContext(
            new DbContextOptionsBuilder<StockmaDbContext>().UseNpgsql(postgres.ConnectionString).Options,
            new Tenancy.TenantContext());

        return await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT r.name AS "Value"
                  FROM users u
                  JOIN user_roles ur ON ur.user_id = u.id
                  JOIN roles r ON r.id = ur.role_id
                 WHERE u.tenant_id = {0};
                """,
                tenantId)
            .ToListAsync();
    }

    [Fact]
    public async Task Bootstrap_OnAnEmptyTenant_CreatesItsFirstTenantAdmin()
    {
        await using var provider = BuildProvider();
        var tenantId = await SeedEmptyTenantAsync();

        await BootstrapAsync(provider, tenantId, $"{Guid.NewGuid():N}@droga.co");

        (await AdminsOfAsync(tenantId)).Should().Equal([TenantRoles.TenantAdmin]);
    }

    [Fact]
    public async Task Bootstrap_InParallel_CreatesExactlyOneAdmin()
    {
        await using var provider = BuildProvider();
        var tenantId = await SeedEmptyTenantAsync();

        var attempts = Enumerable.Range(0, 8)
            .Select(index => Task.Run(() => BootstrapAsync(provider, tenantId, $"admin-{index}-{Guid.NewGuid():N}@droga.co")))
            .ToList();

        var outcome = await Task.WhenAll(attempts.Select(async attempt =>
        {
            try
            {
                await attempt;
                return "ok";
            }
            catch (TenantAlreadyBootstrappedException)
            {
                return "rejected";
            }
        }));

        outcome.Count(result => result == "ok").Should().Be(1, "ADR-017: el chequeo y el alta corren bajo un lock por tenant");
        outcome.Count(result => result == "rejected").Should().Be(7);
        (await AdminsOfAsync(tenantId)).Should().Equal([TenantRoles.TenantAdmin]);
    }

    [Fact]
    public async Task Bootstrap_OnATenantThatAlreadyHasUsers_IsRejected()
    {
        await using var provider = BuildProvider();
        var tenantId = await SeedEmptyTenantAsync();
        await BootstrapAsync(provider, tenantId, $"{Guid.NewGuid():N}@droga.co");

        var act = () => BootstrapAsync(provider, tenantId, $"{Guid.NewGuid():N}@droga.co");

        await act.Should().ThrowAsync<TenantAlreadyBootstrappedException>();
    }

    [Fact]
    public async Task TenantAccountsExists_ForASeededTenant_IsTrue()
    {
        await using var provider = BuildProvider();
        var tenantId = await SeedEmptyTenantAsync();
        await using var scope = provider.CreateAsyncScope();

        (await scope.ServiceProvider.GetRequiredService<ITenantAccounts>().ExistsAsync(tenantId)).Should().BeTrue();
    }

    [Fact]
    public async Task TenantAccountsExists_ForAnUnknownTenant_IsFalse()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        (await scope.ServiceProvider.GetRequiredService<ITenantAccounts>().ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }
}
