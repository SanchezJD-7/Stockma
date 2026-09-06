using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Application.Batches;
using Stockma.Application.Batches.Commands;
using Stockma.Application.Products.Commands;
using Stockma.Domain.Enums;
using Stockma.Domain.Exceptions;
using Stockma.Infrastructure.Batches;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Products;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class BatchStockTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
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

    private async Task<(Guid TenantId, Guid ProductId)> SeedProductAsync()
    {
        var tenantId = await SeedTenantAsync();

        await using var context = CreateContext(tenantId);

        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        var product = await new RegisterProductCommandHandler(
                new ProductRepository(context),
                new SkuGenerator(context),
                tenantContext)
            .Handle(
                new RegisterProductCommand("Acetaminofén 500mg", ProductCategory.Medication),
                CancellationToken.None);

        return (tenantId, product.Id);
    }

    private RegisterBatchCommandHandler CreateRegisterHandler(StockmaDbContext context, Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        return new RegisterBatchCommandHandler(
            new BatchRepository(context),
            new ProductRepository(context),
            tenantContext);
    }

    private static AdjustBatchStockCommandHandler CreateAdjustHandler(StockmaDbContext context) =>
        new(new BatchRepository(context));

    private static DateOnly InSevenMonths => DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(7);

    [Fact]
    public async Task Register_WithValidData_PersistsTheBatch()
    {
        var (tenantId, productId) = await SeedProductAsync();
        await using var context = CreateContext(tenantId);

        var batch = await CreateRegisterHandler(context, tenantId).Handle(
            new RegisterBatchCommand(productId, "LOTE-001", InSevenMonths, 10, "Estante A3"),
            CancellationToken.None);

        batch.LotNumber.Should().Be("LOTE-001");
        batch.CurrentQuantity.Should().Be(10);
        batch.Status.Should().Be(BatchStatus.Active);
    }

    [Fact]
    public async Task Register_WithMissingProduct_ThrowsProductNotFound()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);

        var act = async () => await CreateRegisterHandler(context, tenantId).Handle(
            new RegisterBatchCommand(Guid.NewGuid(), "LOTE-001", InSevenMonths, 10, "Estante A3"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ProductNotFoundForBatchException>(
            "un ProductId inexistente rechaza el alta (FR-012)");
    }

    [Fact]
    public async Task Register_WithProductOfAnotherTenant_ThrowsProductNotFound()
    {
        var (_, productOfA) = await SeedProductAsync();
        var tenantB = await SeedTenantAsync();

        await using var contextB = CreateContext(tenantB);

        var act = async () => await CreateRegisterHandler(contextB, tenantB).Handle(
            new RegisterBatchCommand(productOfA, "LOTE-001", InSevenMonths, 10, "Estante A3"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ProductNotFoundForBatchException>(
            "el ProductId debe pertenecer al mismo tenant");
    }

    [Fact]
    public async Task Adjust_WithPositiveDelta_PersistsTheIncrease()
    {
        var (tenantId, productId) = await SeedProductAsync();
        await using var context = CreateContext(tenantId);

        var batch = await CreateRegisterHandler(context, tenantId).Handle(
            new RegisterBatchCommand(productId, "LOTE-001", InSevenMonths, 10, "Estante A3"),
            CancellationToken.None);

        await CreateAdjustHandler(context).Handle(
            new AdjustBatchStockCommand(batch.Id, 5),
            CancellationToken.None);

        await using var verification = CreateContext(tenantId);
        var persisted = await verification.Batches.SingleAsync(b => b.Id == batch.Id);
        persisted.CurrentQuantity.Should().Be(15, "escenario de aumento de stock de FR-013");
    }

    [Fact]
    public async Task Adjust_BeyondStock_IsRejectedAndNothingIsPersisted()
    {
        var (tenantId, productId) = await SeedProductAsync();
        await using var context = CreateContext(tenantId);

        var batch = await CreateRegisterHandler(context, tenantId).Handle(
            new RegisterBatchCommand(productId, "LOTE-001", InSevenMonths, 3, "Estante A3"),
            CancellationToken.None);

        var act = async () => await CreateAdjustHandler(context).Handle(
            new AdjustBatchStockCommand(batch.Id, -5),
            CancellationToken.None);

        await act.Should().ThrowAsync<NegativeStockException>();
        await using var verification = CreateContext(tenantId);
        var persisted = await verification.Batches.SingleAsync(b => b.Id == batch.Id);
        persisted.CurrentQuantity.Should().Be(3, "el rechazo es total, nunca parcial");
    }

    [Fact]
    public async Task Adjust_OfMissingBatch_ThrowsBatchNotFound()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);

        var act = async () => await CreateAdjustHandler(context).Handle(
            new AdjustBatchStockCommand(Guid.NewGuid(), 5),
            CancellationToken.None);

        await act.Should().ThrowAsync<BatchNotFoundException>();
    }

    [Fact]
    public async Task Adjust_AlwaysAppliesDeltaOverTheFreshValue()
    {
        var (tenantId, productId) = await SeedProductAsync();

        Guid batchId;
        await using (var setup = CreateContext(tenantId))
        {
            var batch = await CreateRegisterHandler(setup, tenantId).Handle(
                new RegisterBatchCommand(productId, "LOTE-001", InSevenMonths, 10, "Estante A3"),
                CancellationToken.None);
            batchId = batch.Id;
        }

        await using var first = CreateContext(tenantId);
        await using var second = CreateContext(tenantId);

        await first.Batches.SingleAsync(b => b.Id == batchId);
        await second.Batches.SingleAsync(b => b.Id == batchId);

        await CreateAdjustHandler(second).Handle(
            new AdjustBatchStockCommand(batchId, 5),
            CancellationToken.None);
        await CreateAdjustHandler(first).Handle(
            new AdjustBatchStockCommand(batchId, 3),
            CancellationToken.None);

        await using var verification = CreateContext(tenantId);
        var persisted = await verification.Batches.SingleAsync(b => b.Id == batchId);
        persisted.CurrentQuantity.Should().Be(
            18,
            "10 + 5 + 3: ningun ajuste se pierde porque el delta se re-aplica sobre el valor recargado");
    }

    [Fact]
    public async Task Adjust_UnderRealConcurrency_LosesNoAdjustment()
    {
        var (tenantId, productId) = await SeedProductAsync();

        Guid batchId;
        await using (var setup = CreateContext(tenantId))
        {
            var batch = await CreateRegisterHandler(setup, tenantId).Handle(
                new RegisterBatchCommand(productId, "LOTE-001", InSevenMonths, 0, "Estante A3"),
                CancellationToken.None);
            batchId = batch.Id;
        }

        const int concurrentAdjustments = 12;

        var tasks = Enumerable.Range(0, concurrentAdjustments).Select(async _ =>
        {
            await using var context = CreateContext(tenantId);
            var repository = new BatchRepository(context, maxRetries: concurrentAdjustments * 3);
            await repository.AdjustAsync(batchId, 1, CancellationToken.None);
        });

        await Task.WhenAll(tasks);

        await using var verification = CreateContext(tenantId);
        var persisted = await verification.Batches.SingleAsync(b => b.Id == batchId);
        persisted.CurrentQuantity.Should().Be(
            concurrentAdjustments,
            "con xmin como token y reintento, ningun incremento concurrente debe perderse (FR-014)");
    }
}
