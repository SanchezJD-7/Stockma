using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class BatchesEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string TenantHeader = "X-Tenant-ID";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    private HttpClient CreateClient(Guid tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());
        return client;
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/products",
            new { name = "Acetaminofén 500mg", category = "Medication" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var product = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return product.GetProperty("id").GetGuid();
    }

    private static object NewBatch(Guid productId, int quantity = 10, int monthsAhead = 7) =>
        new
        {
            productId,
            lotNumber = "LOTE-001",
            expirationDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(monthsAhead),
            initialQuantity = quantity,
            locationShelf = "Estante A3",
        };

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.TryGetProperty("errorCode", out var errorCode) ? errorCode.GetString() : null;
    }

    [Fact]
    public async Task Post_WithValidData_Returns201()
    {
        var client = CreateClient(await SeedTenantAsync());
        var productId = await CreateProductAsync(client);

        var response = await client.PostAsJsonAsync("/api/batches", NewBatch(productId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var batch = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        batch.GetProperty("lotNumber").GetString().Should().Be("LOTE-001");
        batch.GetProperty("currentQuantity").GetInt32().Should().Be(10);
        batch.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Post_WithMissingProduct_Returns404()
    {
        var client = CreateClient(await SeedTenantAsync());

        var response = await client.PostAsJsonAsync("/api/batches", NewBatch(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorCodeAsync(response)).Should().Be("BATCH_PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Adjust_WithPositiveDelta_Returns200AndNewQuantity()
    {
        var client = CreateClient(await SeedTenantAsync());
        var productId = await CreateProductAsync(client);

        var created = await client.PostAsJsonAsync("/api/batches", NewBatch(productId));
        var batch = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var batchId = batch.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync($"/api/batches/{batchId}/adjust", new { delta = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var adjusted = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        adjusted.GetProperty("currentQuantity").GetInt32().Should().Be(15);
    }

    [Fact]
    public async Task Adjust_BeyondStock_Returns422AndLeavesStockUntouched()
    {
        var client = CreateClient(await SeedTenantAsync());
        var productId = await CreateProductAsync(client);

        var created = await client.PostAsJsonAsync("/api/batches", NewBatch(productId, quantity: 3));
        var batch = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var batchId = batch.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync($"/api/batches/{batchId}/adjust", new { delta = -5 });

        response.StatusCode.Should().Be(
            HttpStatusCode.UnprocessableEntity,
            "el request esta bien formado pero es improcesable contra el stock actual (FR-013)");
        (await ReadErrorCodeAsync(response)).Should().Be("BATCH_NEGATIVE_STOCK");

        var check = await client.GetAsync($"/api/batches?productId={productId}");
        var batches = await check.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        batches[0].GetProperty("currentQuantity").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Adjust_OfMissingBatch_Returns404()
    {
        var client = CreateClient(await SeedTenantAsync());

        var response = await client.PostAsJsonAsync(
            $"/api/batches/{Guid.NewGuid()}/adjust",
            new { delta = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorCodeAsync(response)).Should().Be("BATCH_NOT_FOUND");
    }

    [Fact]
    public async Task Get_ByProduct_ReturnsSemaphoreColorAsString()
    {
        var client = CreateClient(await SeedTenantAsync());
        var productId = await CreateProductAsync(client);

        await client.PostAsJsonAsync("/api/batches", NewBatch(productId, monthsAhead: 7));

        var response = await client.GetAsync($"/api/batches?productId={productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var batches = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        batches.GetArrayLength().Should().Be(1);
        batches[0].GetProperty("semaphoreColor").GetString().Should().Be(
            "Green",
            "a 7 meses con umbrales default el lote es verde, y el contrato lo expone como string");
    }

    [Fact]
    public async Task Get_ByProduct_ComputesExpiredForPastDate()
    {
        var client = CreateClient(await SeedTenantAsync());
        var productId = await CreateProductAsync(client);

        await client.PostAsJsonAsync("/api/batches", NewBatch(productId, monthsAhead: -1));

        var response = await client.GetAsync($"/api/batches?productId={productId}");

        var batches = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        batches[0].GetProperty("semaphoreColor").GetString().Should().Be("Expired");
    }

    [Fact]
    public async Task Get_ByProduct_DoesNotCrossTenants()
    {
        var clientA = CreateClient(await SeedTenantAsync());
        var clientB = CreateClient(await SeedTenantAsync());

        var productOfA = await CreateProductAsync(clientA);
        await clientA.PostAsJsonAsync("/api/batches", NewBatch(productOfA));

        var response = await clientB.GetAsync($"/api/batches?productId={productOfA}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var batches = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        batches.GetArrayLength().Should().Be(
            0,
            "un tenant no debe ver los lotes de otro, ni siquiera conociendo el productId (NFR-007)");
    }
}
