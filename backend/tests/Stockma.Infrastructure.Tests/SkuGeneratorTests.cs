using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Products;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class SkuGeneratorTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
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
    private async Task SeedTenantsAsync(params Guid[] tenantIds)
    {
        await using var context = CreateContext(tenantIds[0]);
        await context.Database.MigrateAsync();

        var tenantValues = string.Join(", ", tenantIds.Select(id => $"('{id}')"));
        var settingsValues = string.Join(", ", tenantIds.Select(id => $"('{id}', 2, 6, 3, 1)"));

        await context.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO tenants (id) VALUES {tenantValues} ON CONFLICT DO NOTHING;

            INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number)
            VALUES {settingsValues} ON CONFLICT DO NOTHING;
            """);
    }

    [Fact]
    public async Task NextAsync_ReturnsSequentialSkusWithExpectedFormat()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantsAsync(tenantId);

        await using var context = CreateContext(tenantId);
        var generator = new SkuGenerator(context);

        var first = await generator.NextAsync();
        var second = await generator.NextAsync();

        first.Should().Be("SKU-1", "la secuencia arranca en 1");
        second.Should().Be("SKU-2", "cada llamada consume un número de la secuencia");
    }

    [Fact]
    public async Task NextAsync_KeepsSequencesIndependentPerTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedTenantsAsync(tenantA, tenantB);

        await using var contextA = CreateContext(tenantA);
        await using var contextB = CreateContext(tenantB);

        var generatorA = new SkuGenerator(contextA);
        await generatorA.NextAsync();
        await generatorA.NextAsync();
        var thirdOfA = await generatorA.NextAsync();

        var firstOfB = await new SkuGenerator(contextB).NextAsync();

        thirdOfA.Should().Be("SKU-3");
        firstOfB.Should().Be(
            "SKU-1",
            "la secuencia es por tenant: consumir la de A no debe mover la de B");
    }

    [Fact]
    public async Task NextAsync_UnderConcurrency_NeverRepeatsASku()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantsAsync(tenantId);

        const int concurrentRequests = 20;

        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            await using var context = CreateContext(tenantId);
            return await new SkuGenerator(context).NextAsync();
        });

        var skus = await Task.WhenAll(tasks);

        skus.Should().OnlyHaveUniqueItems(
            "UPDATE ... RETURNING toma lock de fila, así que las altas concurrentes del mismo tenant se serializan y no pueden colisionar (FR-009)");
        skus.Should().HaveCount(concurrentRequests);
    }

    [Fact]
    public async Task NextAsync_FailsLoudlyWhenTenantHasNoSettings()
    {
        var tenantWithoutSettings = Guid.NewGuid();
        await SeedTenantsAsync(Guid.NewGuid());

        await using var context = CreateContext(tenantWithoutSettings);
        var generator = new SkuGenerator(context);

        var act = async () => await generator.NextAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no tiene fila en tenant_settings*");
    }
}
