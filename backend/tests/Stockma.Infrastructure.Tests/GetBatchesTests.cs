using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Application.Batches.Commands;
using Stockma.Application.Batches.Queries;
using Stockma.Application.Products.Commands;
using Stockma.Domain.Enums;
using Stockma.Infrastructure.Batches;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Products;
using Stockma.Infrastructure.Tenancy;
using Stockma.Infrastructure.Tenants;

namespace Stockma.Infrastructure.Tests;

public class GetBatchesTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Today = new(2026, 9, 2);

    private StockmaDbContext CreateContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        return new StockmaDbContext(options, tenantContext);
    }

    private async Task<Guid> SeedTenantAsync(int greenMonths = 6, int yellowMonths = 3)
    {
        var tenantId = Guid.NewGuid();

        await using var context = CreateContext(tenantId);
        await context.Database.MigrateAsync();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenants (id) VALUES ({tenantId}) ON CONFLICT DO NOTHING");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number) VALUES ({tenantId}, 2, {greenMonths}, {yellowMonths}, 1) ON CONFLICT DO NOTHING");

        return tenantId;
    }

    private async Task<(Guid TenantId, Guid ProductId)> SeedProductAsync(
        int greenMonths = 6,
        int yellowMonths = 3)
    {
        var tenantId = await SeedTenantAsync(greenMonths, yellowMonths);

        await using var context = CreateContext(tenantId);
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        var product = await new RegisterProductCommandHandler(
                new ProductRepository(context),
                new SkuGenerator(context),
                tenantContext)
            .Handle(
                new RegisterProductCommand("Acetaminofén", ProductCategory.Medication),
                CancellationToken.None);

        return (tenantId, product.Id);
    }

    private async Task AddBatchAsync(Guid tenantId, Guid productId, string lotNumber, DateOnly expiration)
    {
        await using var context = CreateContext(tenantId);
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        await new RegisterBatchCommandHandler(
                new BatchRepository(context),
                new ProductRepository(context),
                tenantContext)
            .Handle(
                new RegisterBatchCommand(productId, lotNumber, expiration, 10, "Estante A3"),
                CancellationToken.None);
    }

    private GetBatchesQueryHandler CreateHandler(StockmaDbContext context) =>
        new(new BatchRepository(context), new TenantSettingsProvider(context), new FixedTimeProvider(Today));

    [Fact]
    public async Task Query_ComputesGreenForFarExpiration()
    {
        var (tenantId, productId) = await SeedProductAsync();
        await AddBatchAsync(tenantId, productId, "LOTE-VERDE", Today.AddMonths(7));

        await using var context = CreateContext(tenantId);
        var batches = await CreateHandler(context)
            .Handle(new GetBatchesQuery(productId), CancellationToken.None);

        batches.Should().ContainSingle();
        batches[0].SemaphoreColor.Should().Be(SemaphoreColor.Green);
    }

    [Fact]
    public async Task Query_ComputesExpiredForPastDate()
    {
        var (tenantId, productId) = await SeedProductAsync();
        await AddBatchAsync(tenantId, productId, "LOTE-VENCIDO", Today.AddDays(-1));

        await using var context = CreateContext(tenantId);
        var batches = await CreateHandler(context)
            .Handle(new GetBatchesQuery(productId), CancellationToken.None);

        batches[0].SemaphoreColor.Should().Be(SemaphoreColor.Expired);
    }

    [Fact]
    public async Task Query_UsesTenantThresholds()
    {
        var (tenantId, productId) = await SeedProductAsync(greenMonths: 9, yellowMonths: 3);
        await AddBatchAsync(tenantId, productId, "LOTE-7M", Today.AddMonths(7));

        await using var context = CreateContext(tenantId);
        var batches = await CreateHandler(context)
            .Handle(new GetBatchesQuery(productId), CancellationToken.None);

        batches[0].SemaphoreColor.Should().Be(
            SemaphoreColor.Yellow,
            "el color se evalúa con los umbrales del tenant, no con los defaults");
    }

    [Fact]
    public async Task Query_OrdersByExpirationDate()
    {
        var (tenantId, productId) = await SeedProductAsync();
        await AddBatchAsync(tenantId, productId, "LOTE-LEJANO", Today.AddMonths(10));
        await AddBatchAsync(tenantId, productId, "LOTE-CERCANO", Today.AddMonths(2));

        await using var context = CreateContext(tenantId);
        var batches = await CreateHandler(context)
            .Handle(new GetBatchesQuery(productId), CancellationToken.None);

        batches.Should().HaveCount(2);
        batches[0].LotNumber.Should().Be(
            "LOTE-CERCANO",
            "el orden por vencimiento es la base del FEFO diferido al slice 2 (NFR-009)");
    }

    [Fact]
    public async Task Query_DoesNotPersistTheColor()
    {
        var (tenantId, productId) = await SeedProductAsync();
        await AddBatchAsync(tenantId, productId, "LOTE-001", Today.AddMonths(7));

        await using var context = CreateContext(tenantId);
        await CreateHandler(context).Handle(new GetBatchesQuery(productId), CancellationToken.None);

        var columns = await context.Database
            .SqlQuery<string>($"SELECT column_name FROM information_schema.columns WHERE table_name = 'batches'")
            .ToListAsync();

        columns.Should().NotContain(
            column => column.Contains("color", StringComparison.OrdinalIgnoreCase),
            "la semaforización se computa en lectura y nunca se persiste");
    }
}
