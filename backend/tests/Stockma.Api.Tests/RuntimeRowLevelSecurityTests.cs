using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class RuntimeRowLevelSecurityTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private async Task<Guid> SeedTenantWithAProductAsync()
    {
        var tenantId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenants (id) VALUES ({tenantId}) ON CONFLICT DO NOTHING");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number) VALUES ({tenantId}, 2, 6, 3, 1) ON CONFLICT DO NOTHING");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO products (id, tenant_id, sku, name, category, currency) VALUES ({Guid.NewGuid()}, {tenantId}, {"SKU-" + tenantId.ToString("N")[..8]}, 'Ibuprofeno', 1, 'COP')");

        await scope.ServiceProvider.GetRequiredService<IUserAccounts>().CreateAsync(new NewUser(
            tenantId,
            $"rls-{Guid.NewGuid():N}@droga.co",
            "Contrasena-Larga-1",
            "+5491100000077",
            TenantRoles.Member));

        return tenantId;
    }

    [Fact]
    public async Task TheRuntimeConnection_UsesTheRestrictedAppUser()
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(Guid.NewGuid());
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        var role = await context.Database.SqlQueryRaw<string>("SELECT current_user::text AS \"Value\"").SingleAsync();

        role.Should().Be(
            "app_user",
            "ADR-017: el runtime de la API nunca usa el rol propietario ni un superusuario, o la RLS no filtra");
    }

    [Fact]
    public async Task WithTheEfFilterBypassed_RowLevelSecurityAloneHidesAnotherTenantsRows()
    {
        var tenantA = await SeedTenantWithAProductAsync();
        var tenantB = await SeedTenantWithAProductAsync();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantA);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        var productTenants = await context.Products.IgnoreQueryFilters().Select(p => p.TenantId).Distinct().ToListAsync();
        var userTenants = await context.Users.IgnoreQueryFilters().Select(u => u.TenantId).Distinct().ToListAsync();
        var settingsTenants = await context.TenantSettings.IgnoreQueryFilters().Select(s => s.TenantId).ToListAsync();

        productTenants.Should().Equal([tenantA], "NFR-001: aun sin el filtro de EF, la base no devuelve filas de otro tenant");
        userTenants.Should().Equal([tenantA]);
        settingsTenants.Should().Equal([tenantA]);
        productTenants.Should().NotContain(tenantB);
    }

    [Fact]
    public async Task WithTheEfFilterBypassed_RowLevelSecurityRejectsWritingIntoAnotherTenant()
    {
        var tenantA = await SeedTenantWithAProductAsync();
        var tenantB = await SeedTenantWithAProductAsync();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantA);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        var act = () => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO products (id, tenant_id, sku, name, category, currency) VALUES ({Guid.NewGuid()}, {tenantB}, 'SKU-INTRUSO', 'Intruso', 1, 'COP')");

        (await act.Should().ThrowAsync<Npgsql.PostgresException>())
            .Which.SqlState.Should().Be("42501", "la política también actúa como WITH CHECK: no se escribe en otro tenant");
    }

    [Fact]
    public async Task ATenantAToken_ListingProducts_NeverSeesTenantBRows()
    {
        var tenantA = await SeedTenantWithAProductAsync();
        await SeedTenantWithAProductAsync();

        var response = await factory.CreateAuthenticatedClient(tenantA).GetAsync("/api/products?query=Ibuprofeno");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SKU-" + tenantA.ToString("N")[..8]);
        body.Split("Ibuprofeno").Length.Should().Be(2, "sólo el producto del tenant A viaja en la respuesta");
    }
}
