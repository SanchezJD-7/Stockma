using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Application.Products.Commands;
using Stockma.Application.Products.Exceptions;
using Stockma.Application.Products.Queries;
using Stockma.Domain.Enums;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Products;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;
public class ProductSearchTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
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

    private static RegisterProductCommandHandler CreateRegisterHandler(StockmaDbContext context)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(context.CurrentTenantId);

        return new RegisterProductCommandHandler(
            new ProductRepository(context),
            new SkuGenerator(context),
            tenantContext);
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

    private async Task<Guid> SeedCatalogAsync(params string[] names)
    {
        var tenantId = await SeedTenantAsync();

        await using var context = CreateContext(tenantId);
        var handler = CreateRegisterHandler(context);

        foreach (var name in names)
        {
            await handler.Handle(
                new RegisterProductCommand(name, ProductCategory.Medication),
                CancellationToken.None);
        }

        return tenantId;
    }

    private SearchProductsQueryHandler CreateSearchHandler(StockmaDbContext context) =>
        new(new ProductRepository(context));

    private GetProductByBarcodeQueryHandler CreateBarcodeHandler(StockmaDbContext context) =>
        new(new ProductRepository(context));

    [Fact]
    public async Task Search_MatchesPartialPrefixInsideTheName()
    {
        var tenantId = await SeedCatalogAsync("Acetaminofén 500mg", "Ibuprofeno 400mg", "Loratadina 10mg");

        await using var context = CreateContext(tenantId);

        var results = await CreateSearchHandler(context)
            .Handle(new SearchProductsQuery("aceta"), CancellationToken.None);

        results.Should().ContainSingle(
            "trigram matchea substrings, que es lo que necesita el autocompletado");
        results[0].Name.Should().Be("Acetaminofén 500mg");
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        var tenantId = await SeedCatalogAsync("Acetaminofén 500mg");

        await using var context = CreateContext(tenantId);

        var results = await CreateSearchHandler(context)
            .Handle(new SearchProductsQuery("ACETA"), CancellationToken.None);

        results.Should().ContainSingle("el filtro usa ILIKE");
    }

    [Fact]
    public async Task Search_OrdersByRelevance()
    {
        var tenantId = await SeedCatalogAsync("Ibuprofeno", "Ibuprofeno 400mg con cafeína");

        await using var context = CreateContext(tenantId);

        var results = await CreateSearchHandler(context)
            .Handle(new SearchProductsQuery("Ibuprofeno"), CancellationToken.None);

        results.Should().HaveCount(2);
        results[0].Name.Should().Be(
            "Ibuprofeno",
            "el nombre exacto tiene mayor similarity que uno con texto extra");
    }

    [Fact]
    public async Task Search_RespectsTheLimit()
    {
        var tenantId = await SeedCatalogAsync("Vitamina A", "Vitamina B", "Vitamina C", "Vitamina D");

        await using var context = CreateContext(tenantId);

        var results = await CreateSearchHandler(context)
            .Handle(new SearchProductsQuery("Vitamina", Limit: 2), CancellationToken.None);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_IsScopedToTheActiveTenant()
    {
        var tenantA = await SeedCatalogAsync("Acetaminofén de A");
        var tenantB = await SeedCatalogAsync("Acetaminofén de B");

        await using var contextB = CreateContext(tenantB);

        var results = await CreateSearchHandler(contextB)
            .Handle(new SearchProductsQuery("Acetaminofén"), CancellationToken.None);

        results.Should().ContainSingle("la búsqueda está acotada al tenant activo (NFR-007)");
        results[0].Name.Should().Be("Acetaminofén de B");
        tenantA.Should().NotBe(tenantB);
    }

    [Fact]
    public async Task Search_WithBlankQuery_ReturnsEmpty()
    {
        var tenantId = await SeedCatalogAsync("Acetaminofén");

        await using var context = CreateContext(tenantId);

        var results = await CreateSearchHandler(context)
            .Handle(new SearchProductsQuery("   "), CancellationToken.None);

        results.Should().BeEmpty("una consulta vacía no debe devolver el catálogo entero");
    }

    [Fact]
    public async Task GetByBarcode_ReturnsTheProduct()
    {
        var tenantId = await SeedTenantAsync();

        await using var context = CreateContext(tenantId);
        await CreateRegisterHandler(context).Handle(
            new RegisterProductCommand("Acetaminofén", ProductCategory.Medication, Barcode: "7701234567890"),
            CancellationToken.None);

        var product = await CreateBarcodeHandler(context)
            .Handle(new GetProductByBarcodeQuery("7701234567890"), CancellationToken.None);

        product.Name.Should().Be("Acetaminofén");
    }

    [Fact]
    public async Task GetByBarcode_WhenMissing_ThrowsNotFound()
    {
        var tenantId = await SeedTenantAsync();

        await using var context = CreateContext(tenantId);

        var act = async () => await CreateBarcodeHandler(context)
            .Handle(new GetProductByBarcodeQuery("0000000000000"), CancellationToken.None);

        await act.Should().ThrowAsync<BarcodeNotFoundException>("el lookup responde 404 (FR-011)");
    }
}
