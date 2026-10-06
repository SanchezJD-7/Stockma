using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;
using Stockma.Infrastructure.Tenants;

namespace Stockma.Infrastructure.Tests;

public class TenantSecondFactorPolicyTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private StockmaDbContext CreateContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        return new StockmaDbContext(options, tenantContext);
    }

    private async Task<Guid> SeedTenantAsync(bool withSettings = true)
    {
        var tenantId = Guid.NewGuid();

        await using var context = CreateContext(tenantId);
        await context.Database.MigrateAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenants (id) VALUES ({tenantId}) ON CONFLICT DO NOTHING");

        if (withSettings)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number) VALUES ({tenantId}, 2, 6, 3, 1) ON CONFLICT DO NOTHING");
        }

        return tenantId;
    }

    [Fact]
    public async Task RequireSecondFactor_WithoutAnySettingsRow_ReturnsTrue()
    {
        var tenantId = await SeedTenantAsync(withSettings: false);

        await using var context = CreateContext(tenantId);
        var required = await new TenantSettingsProvider(context).RequireSecondFactorAsync();

        required.Should().BeTrue("T090: fail-closed, sin fila de configuración el 2FA sigue exigido");
    }

    [Fact]
    public async Task RequireSecondFactor_OnAFreshlyMigratedRow_ReturnsTrue()
    {
        var tenantId = await SeedTenantAsync();

        await using var context = CreateContext(tenantId);
        var required = await new TenantSettingsProvider(context).RequireSecondFactorAsync();

        required.Should().BeTrue("la columna nace en true (AddRequireSecondFactor)");
    }

    [Fact]
    public async Task RequireSecondFactor_AfterTurningItOff_ReturnsFalse()
    {
        var tenantId = await SeedTenantAsync();

        await using (var write = CreateContext(tenantId))
        {
            await write.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tenant_settings SET require_second_factor = false WHERE tenant_id = {tenantId}");
        }

        await using var read = CreateContext(tenantId);
        var required = await new TenantSettingsProvider(read).RequireSecondFactorAsync();

        required.Should().BeFalse();
    }

    [Fact]
    public async Task RequireSecondFactor_DoesNotLeakAcrossTenants()
    {
        var turnedOff = await SeedTenantAsync();
        var untouched = await SeedTenantAsync();

        await using (var write = CreateContext(turnedOff))
        {
            await write.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tenant_settings SET require_second_factor = false WHERE tenant_id = {turnedOff}");
        }

        await using var readOther = CreateContext(untouched);
        var required = await new TenantSettingsProvider(readOther).RequireSecondFactorAsync();

        required.Should().BeTrue("T090: apagar el 2FA de un tenant no lo apaga del otro");
    }
}
