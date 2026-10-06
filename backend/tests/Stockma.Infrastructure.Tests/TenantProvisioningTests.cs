using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Infrastructure.DependencyInjection;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenants;

namespace Stockma.Infrastructure.Tests;

public class TenantProvisioningTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
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

    private static async Task ProvisionAsync(IServiceProvider provider, Guid tenantId, bool requireSecondFactor)
    {
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        await scope.ServiceProvider.GetRequiredService<ITenantAccounts>()
            .ProvisionAsync(tenantId, requireSecondFactor);
    }

    private static async Task<bool> ReadSecondFactorAsync(IServiceProvider provider, Guid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        return await new TenantSettingsProvider(scope.ServiceProvider.GetRequiredService<StockmaDbContext>())
            .RequireSecondFactorAsync();
    }

    private static async Task<int> CountSettingsAsync(IServiceProvider provider, Guid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        return await context.TenantSettings.CountAsync(settings => settings.TenantId == tenantId);
    }

    [Fact]
    public async Task Provision_ForAnUnknownTenant_CreatesItWithoutSecondFactor()
    {
        await using var provider = BuildProvider();
        var tenantId = Guid.NewGuid();

        await ProvisionAsync(provider, tenantId, requireSecondFactor: false);

        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        (await context.Tenants.AnyAsync(tenant => tenant.Id == tenantId))
            .Should()
            .BeTrue("T091: el seed da de alta el tenant, no sólo su configuración");
        (await ReadSecondFactorAsync(provider, tenantId))
            .Should()
            .BeFalse("T091: sin este flag apagado el visitante del demo nunca puede entrar");
    }

    [Fact]
    public async Task Provision_RunTwice_KeepsASingleSettingsRow()
    {
        await using var provider = BuildProvider();
        var tenantId = Guid.NewGuid();

        await ProvisionAsync(provider, tenantId, requireSecondFactor: false);
        await ProvisionAsync(provider, tenantId, requireSecondFactor: false);

        (await CountSettingsAsync(provider, tenantId))
            .Should()
            .Be(1, "el seed es idempotente");
        (await ReadSecondFactorAsync(provider, tenantId)).Should().BeFalse();
    }

    [Fact]
    public async Task Provision_AfterTheFlagWasTurnedOn_TurnsItOffAgain()
    {
        await using var provider = BuildProvider();
        var tenantId = Guid.NewGuid();

        await ProvisionAsync(provider, tenantId, requireSecondFactor: true);
        await ProvisionAsync(provider, tenantId, requireSecondFactor: false);

        (await ReadSecondFactorAsync(provider, tenantId))
            .Should()
            .BeFalse("re-correr el seed restaura el estado demo, no lo pisa a medias");
        (await CountSettingsAsync(provider, tenantId)).Should().Be(1);
    }
}
