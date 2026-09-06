using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;
public class ProductsEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string TenantHeader = "X-Tenant-ID";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private async Task<Guid> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenants (id) VALUES ({tenantId}) ON CONFLICT DO NOTHING");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number) VALUES ({tenantId}, 2, 6, 3, 1) ON CONFLICT DO NOTHING");

        return tenantId;
    }

    private HttpClient CreateClient(Guid? tenantId)
    {
        var client = factory.CreateClient();

        if (tenantId.HasValue)
        {
            client.DefaultRequestHeaders.Add(TenantHeader, tenantId.Value.ToString());
        }

        return client;
    }

    private HttpClient CreateClientWithRawHeader(string headerValue)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, headerValue);
        return client;
    }

    private static object NewProduct(
        string name = "Acetaminofén 500mg",
        string category = "Medication",
        string? barcode = null,
        string? sku = null) =>
        new { name, category, barcode, sku };

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.TryGetProperty("errorCode", out var errorCode)
            ? errorCode.GetString()
            : null;
    }

    [Fact]
    public async Task Post_WithoutSku_Returns201WithGeneratedSku()
    {
        var client = CreateClient(await SeedTenantAsync());

        var response = await client.PostAsJsonAsync("/api/products", NewProduct());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var product = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        product.GetProperty("sku").GetString().Should().Be("SKU-1");
        product.GetProperty("name").GetString().Should().Be("Acetaminofén 500mg");
    }

    [Fact]
    public async Task Post_SerializesCategoryAsString()
    {
        var client = CreateClient(await SeedTenantAsync());

        var response = await client.PostAsJsonAsync("/api/products", NewProduct(category: "Supplement"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var product = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        product.GetProperty("category").GetString().Should().Be(
            "Supplement",
            "el contrato expone la categoría como string, no como número");
    }

    [Fact]
    public async Task Post_WithDuplicateSku_Returns409WithErrorCode()
    {
        var client = CreateClient(await SeedTenantAsync());

        await client.PostAsJsonAsync("/api/products", NewProduct(sku: "DUP-1"));
        var response = await client.PostAsJsonAsync("/api/products", NewProduct(sku: "DUP-1"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(response)).Should().Be("PRODUCT_SKU_DUPLICATE");
    }

    [Fact]
    public async Task Post_WithDuplicateBarcode_Returns409WithErrorCode()
    {
        var client = CreateClient(await SeedTenantAsync());

        await client.PostAsJsonAsync("/api/products", NewProduct(barcode: "7701111111111"));
        var response = await client.PostAsJsonAsync("/api/products", NewProduct(barcode: "7701111111111"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(response)).Should().Be("PRODUCT_BARCODE_DUPLICATE");
    }

    [Fact]
    public async Task Put_WithDifferentSku_Returns400WithErrorCode()
    {
        var client = CreateClient(await SeedTenantAsync());

        var created = await client.PostAsJsonAsync("/api/products", NewProduct());
        var product = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = product.GetProperty("id").GetString();

        var response = await client.PutAsJsonAsync(
            $"/api/products/{id}",
            new { name = "Otro nombre", category = "Medication", sku = "SKU-CAMBIADO" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorCodeAsync(response)).Should().Be("PRODUCT_SKU_IMMUTABLE");
    }

    [Fact]
    public async Task Put_Valid_Returns200AndKeepsSku()
    {
        var client = CreateClient(await SeedTenantAsync());

        var created = await client.PostAsJsonAsync("/api/products", NewProduct());
        var product = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = product.GetProperty("id").GetString();
        var originalSku = product.GetProperty("sku").GetString();

        var response = await client.PutAsJsonAsync(
            $"/api/products/{id}",
            new { name = "Nombre actualizado", category = "PersonalCare" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        updated.GetProperty("name").GetString().Should().Be("Nombre actualizado");
        updated.GetProperty("category").GetString().Should().Be("PersonalCare");
        updated.GetProperty("sku").GetString().Should().Be(originalSku, "el SKU es inmutable (FR-010)");
    }

    [Fact]
    public async Task Put_OfMissingProduct_Returns404()
    {
        var client = CreateClient(await SeedTenantAsync());

        var response = await client.PutAsJsonAsync(
            $"/api/products/{Guid.NewGuid()}",
            new { name = "Fantasma", category = "Medication" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorCodeAsync(response)).Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Get_SearchByName_ReturnsMatches()
    {
        var client = CreateClient(await SeedTenantAsync());

        await client.PostAsJsonAsync("/api/products", NewProduct("Acetaminofén 500mg"));
        await client.PostAsJsonAsync("/api/products", NewProduct("Ibuprofeno 400mg"));

        var response = await client.GetAsync("/api/products?query=aceta");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var matches = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        matches.GetArrayLength().Should().Be(1);
        matches[0].GetProperty("name").GetString().Should().Be("Acetaminofén 500mg");
    }

    [Fact]
    public async Task Get_ByBarcode_ReturnsTheProduct()
    {
        var client = CreateClient(await SeedTenantAsync());

        await client.PostAsJsonAsync("/api/products", NewProduct(barcode: "7702222222222"));

        var response = await client.GetAsync("/api/products/by-barcode/7702222222222");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var product = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        product.GetProperty("barcode").GetString().Should().Be("7702222222222");
    }

    [Fact]
    public async Task Get_ByMissingBarcode_Returns404WithErrorCode()
    {
        var client = CreateClient(await SeedTenantAsync());

        var response = await client.GetAsync("/api/products/by-barcode/0000000000000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorCodeAsync(response)).Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Request_WithoutTenantHeader_Returns400()
    {
        var client = CreateClient(tenantId: null);

        var response = await client.GetAsync("/api/products?query=aceta");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorCodeAsync(response)).Should().Be("TENANT_HEADER_MISSING");
    }

    [Fact]
    public async Task Request_WithInvalidTenantHeader_Returns400()
    {
        var client = CreateClientWithRawHeader("no-es-un-guid");

        var response = await client.GetAsync("/api/products?query=aceta");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorCodeAsync(response)).Should().Be("TENANT_HEADER_INVALID");
    }

    [Fact]
    public async Task Get_ByBarcode_DoesNotCrossTenants()
    {
        var clientA = CreateClient(await SeedTenantAsync());
        var clientB = CreateClient(await SeedTenantAsync());

        await clientA.PostAsJsonAsync("/api/products", NewProduct(barcode: "7703333333333"));

        var response = await clientB.GetAsync("/api/products/by-barcode/7703333333333");

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "un tenant no debe alcanzar el barcode de otro por HTTP (NFR-007)");
    }
}
