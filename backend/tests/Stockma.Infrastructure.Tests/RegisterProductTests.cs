using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Application.Products.Commands;
using Stockma.Application.Products.Exceptions;
using Stockma.Domain.Enums;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Products;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;
public class RegisterProductTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
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

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO tenants (id) VALUES ('" + tenantId + "') ON CONFLICT DO NOTHING;");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number) "
            + "VALUES ('" + tenantId + "', 2, 6, 3, 1) ON CONFLICT DO NOTHING;");

        return tenantId;
    }

    private static RegisterProductCommandHandler CreateRegisterHandler(StockmaDbContext context)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(context.CurrentTenantId);

        return new RegisterProductCommandHandler(
            new ProductRepository(context),
            new SkuGenerator(context),
            tenantContext);
    }

    private static UpdateProductCommandHandler CreateUpdateHandler(StockmaDbContext context) =>
        new(new ProductRepository(context));

    [Fact]
    public async Task Register_WithoutSku_AssignsAutomaticSku()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);

        var result = await CreateRegisterHandler(context).Handle(
            new RegisterProductCommand("Acetaminofén 500mg", ProductCategory.Medication),
            CancellationToken.None);

        result.Sku.Should().Be("SKU-1", "sin SKU explícito el sistema debe generarlo (FR-009)");
        result.Barcode.Should().BeNull();
    }

    [Fact]
    public async Task Register_WithExplicitSku_KeepsIt()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);

        var result = await CreateRegisterHandler(context).Handle(
            new RegisterProductCommand("Ibuprofeno", ProductCategory.Medication, Sku: "MANUAL-1"),
            CancellationToken.None);

        result.Sku.Should().Be("MANUAL-1");
    }

    [Fact]
    public async Task Register_WithDuplicateSku_ThrowsSkuDuplicate()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);
        var handler = CreateRegisterHandler(context);

        await handler.Handle(
            new RegisterProductCommand("Primero", ProductCategory.Medication, Sku: "DUP-1"),
            CancellationToken.None);

        var act = async () => await handler.Handle(
            new RegisterProductCommand("Segundo", ProductCategory.Medication, Sku: "DUP-1"),
            CancellationToken.None);

        await act.Should().ThrowAsync<SkuDuplicateException>(
            "el SKU es único por tenant y responde 409 (FR-009)");
    }

    [Fact]
    public async Task Register_WithDuplicateBarcode_ThrowsBarcodeDuplicate()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);
        var handler = CreateRegisterHandler(context);

        await handler.Handle(
            new RegisterProductCommand("Primero", ProductCategory.Medication, Barcode: "7701234567890"),
            CancellationToken.None);

        var act = async () => await handler.Handle(
            new RegisterProductCommand("Segundo", ProductCategory.Medication, Barcode: "7701234567890"),
            CancellationToken.None);

        await act.Should().ThrowAsync<BarcodeDuplicateException>();
    }

    [Fact]
    public async Task Register_AllowsSeveralProductsWithoutBarcode()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);
        var handler = CreateRegisterHandler(context);

        await handler.Handle(new RegisterProductCommand("Uno", ProductCategory.Supplement), CancellationToken.None);

        var act = async () => await handler.Handle(
            new RegisterProductCommand("Dos", ProductCategory.Supplement),
            CancellationToken.None);

        await act.Should().NotThrowAsync(
            "el índice de barcode es parcial: varios productos sin barcode no deben chocar");
    }

    [Fact]
    public async Task Register_SameSkuInDifferentTenants_IsAllowed()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();

        await using var contextA = CreateContext(tenantA);
        await CreateRegisterHandler(contextA).Handle(
            new RegisterProductCommand("Producto de A", ProductCategory.Medication, Sku: "SHARED-1"),
            CancellationToken.None);

        await using var contextB = CreateContext(tenantB);
        var act = async () => await CreateRegisterHandler(contextB).Handle(
            new RegisterProductCommand("Producto de B", ProductCategory.Medication, Sku: "SHARED-1"),
            CancellationToken.None);

        await act.Should().NotThrowAsync("la unicidad de SKU es por tenant, no global");
    }

    [Fact]
    public async Task Update_ChangesFieldsAndKeepsSku()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);

        var created = await CreateRegisterHandler(context).Handle(
            new RegisterProductCommand("Nombre viejo", ProductCategory.Medication),
            CancellationToken.None);

        var updated = await CreateUpdateHandler(context).Handle(
            new UpdateProductCommand(created.Id, "Nombre nuevo", ProductCategory.Supplement),
            CancellationToken.None);

        updated.Name.Should().Be("Nombre nuevo");
        updated.Category.Should().Be(ProductCategory.Supplement);
        updated.Sku.Should().Be(created.Sku, "el SKU se conserva en el update (FR-010)");
    }

    [Fact]
    public async Task Update_WithDifferentSku_ThrowsSkuImmutable()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);

        var created = await CreateRegisterHandler(context).Handle(
            new RegisterProductCommand("Producto", ProductCategory.Medication),
            CancellationToken.None);

        var act = async () => await CreateUpdateHandler(context).Handle(
            new UpdateProductCommand(created.Id, "Producto", ProductCategory.Medication, Sku: "OTRO-SKU"),
            CancellationToken.None);

        await act.Should().ThrowAsync<SkuImmutableException>(
            "el intento de cambiar el SKU se rechaza con 400 (FR-010)");
    }

    [Fact]
    public async Task Update_OfMissingProduct_ThrowsProductNotFound()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = CreateContext(tenantId);

        var act = async () => await CreateUpdateHandler(context).Handle(
            new UpdateProductCommand(Guid.NewGuid(), "Fantasma", ProductCategory.Medication),
            CancellationToken.None);

        await act.Should().ThrowAsync<ProductNotFoundException>();
    }
}
