using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;
using Stockma.Infrastructure.Tenants;

namespace Stockma.Infrastructure.Tests;

public class TenantBrandingPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
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

    private async Task<Guid> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();

        await using var context = CreateContext(tenantId);
        await context.Database.MigrateAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenants (id) VALUES ({tenantId}) ON CONFLICT DO NOTHING");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number) VALUES ({tenantId}, 2, 6, 3, 1) ON CONFLICT DO NOTHING");

        return tenantId;
    }

    [Fact]
    public async Task GetBranding_WithoutConfiguration_ReturnsNull()
    {
        var tenantId = await SeedTenantAsync();

        await using var context = CreateContext(tenantId);
        var branding = await new TenantSettingsProvider(context).GetBrandingAsync();

        branding.Should().BeNull();
    }

    [Fact]
    public async Task GetBranding_AfterUpdate_ReturnsTheTenantColors()
    {
        var tenantId = await SeedTenantAsync();

        await using (var write = CreateContext(tenantId))
        {
            var settings = await write.TenantSettings.SingleAsync();
            settings.UpdateBranding(new TenantBranding("#8e24aa", "#6a1b9a", "#f3e5f5"));
            await write.SaveChangesAsync();
        }

        await using var read = CreateContext(tenantId);
        var branding = await new TenantSettingsProvider(read).GetBrandingAsync();

        branding.Should().NotBeNull();
        branding!.Primary.Should().Be("#8e24aa");
        branding.PrimaryActive.Should().Be("#6a1b9a");
        branding.PrimaryBg.Should().Be("#f3e5f5");
    }

    [Fact]
    public async Task GetBranding_AfterClearing_ReturnsNullAgain()
    {
        var tenantId = await SeedTenantAsync();

        await using (var write = CreateContext(tenantId))
        {
            var settings = await write.TenantSettings.SingleAsync();
            settings.UpdateBranding(new TenantBranding("#8e24aa", "#6a1b9a", "#f3e5f5"));
            await write.SaveChangesAsync();
        }

        await using (var clear = CreateContext(tenantId))
        {
            var settings = await clear.TenantSettings.SingleAsync();
            settings.UpdateBranding(null);
            await clear.SaveChangesAsync();
        }

        await using var read = CreateContext(tenantId);
        (await new TenantSettingsProvider(read).GetBrandingAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetBranding_DoesNotLeakAcrossTenants()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();

        await using (var write = CreateContext(tenantA))
        {
            var settings = await write.TenantSettings.SingleAsync();
            settings.UpdateBranding(new TenantBranding("#8e24aa", "#6a1b9a", "#f3e5f5"));
            await write.SaveChangesAsync();
        }

        await using var readB = CreateContext(tenantB);
        (await new TenantSettingsProvider(readB).GetBrandingAsync()).Should().BeNull();
    }
}
