using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class TenantBrandingEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string TenantHeader = "X-Tenant-ID";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<Guid> SeedTenantAsync(TenantBranding? branding = null)
    {
        var tenantId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenants (id) VALUES ({tenantId}) ON CONFLICT DO NOTHING");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number) VALUES ({tenantId}, 2, 6, 3, 1) ON CONFLICT DO NOTHING");

        if (branding is not null)
        {
            var settings = await context.TenantSettings.SingleAsync();
            settings.UpdateBranding(branding);
            await context.SaveChangesAsync();
        }

        return tenantId;
    }

    private HttpClient CreateClient(Guid tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());
        return client;
    }

    [Fact]
    public async Task GetBranding_WithoutConfiguration_ReturnsNullSoTheFrontendUsesTokenDefaults()
    {
        var tenantId = await SeedTenantAsync();

        var response = await CreateClient(tenantId).GetAsync("/api/tenant/branding");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Trim().Should().Be("null");
    }

    [Fact]
    public async Task GetBranding_WithConfiguration_ReturnsTheThreeColors()
    {
        var tenantId = await SeedTenantAsync(new TenantBranding("#8e24aa", "#6a1b9a", "#f3e5f5"));

        var response = await CreateClient(tenantId).GetAsync("/api/tenant/branding");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("primary").GetString().Should().Be("#8e24aa");
        body.GetProperty("primaryActive").GetString().Should().Be("#6a1b9a");
        body.GetProperty("primaryBg").GetString().Should().Be("#f3e5f5");
    }

    [Fact]
    public async Task GetBranding_WithoutTenantHeader_ReturnsBadRequest()
    {
        var response = await factory.CreateClient().GetAsync("/api/tenant/branding");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
