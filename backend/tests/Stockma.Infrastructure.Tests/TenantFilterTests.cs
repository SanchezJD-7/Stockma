using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class TenantFilterTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private StockmaDbContext CreateContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        return new StockmaDbContext(options, tenantContext);
    }

    private async Task SeedBothTenantsAsync()
    {
        await using var context = CreateContext(TenantA);
        await context.Database.EnsureCreatedAsync();

        await context.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO tenants (id) VALUES ('{TenantA}'), ('{TenantB}') ON CONFLICT DO NOTHING;

            INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months)
            VALUES ('{TenantA}', 2, 6, 3), ('{TenantB}', 5, 9, 4) ON CONFLICT DO NOTHING;
            """);
    }

    [Fact]
    public async Task FilteredQuery_TenantAOnlySeesItsOwnRows()
    {
        await SeedBothTenantsAsync();

        await using var context = CreateContext(TenantA);
        var settings = await context.TenantSettings.ToListAsync();

        settings.Should().HaveCount(1, "el filtro global debe acotar la consulta al tenant activo");
        settings[0].TenantId.Should().Be(TenantA);
    }

    [Fact]
    public async Task FilteredQuery_TenantBDoesNotSeeTenantAData()
    {
        await SeedBothTenantsAsync();

        await using var context = CreateContext(TenantB);
        var settings = await context.TenantSettings.ToListAsync();

        settings.Should().HaveCount(1);
        settings[0].TenantId.Should().Be(TenantB);
        settings[0].MaxTrustedDevices.Should().Be(5, "debe traer la configuración de B, no la de A");
    }
}
