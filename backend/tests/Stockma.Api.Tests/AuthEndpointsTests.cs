using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class AuthEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
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

    private HttpClient CreateTenantClient(Guid tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());
        return client;
    }

    private static object RegisterBody(string email) => new
    {
        email,
        password = "Contrasena-Larga-1",
        phoneNumber = "+5491100000000",
    };

    private async Task<string> RegisterUserAsync(Guid tenantId, string email)
    {
        var response = await CreateTenantClient(tenantId).PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return email;
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_Returns409WithTheErrorCode()
    {
        var tenantId = await SeedTenantAsync();
        var email = $"dup-{Guid.NewGuid():N}@droga.co";

        await RegisterUserAsync(tenantId, email);

        var response = await CreateTenantClient(tenantId).PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("errorCode").GetString().Should().Be("AUTH_EMAIL_DUPLICATE");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401WithTheUniformErrorCode()
    {
        var tenantId = await SeedTenantAsync();
        var email = $"wrong-{Guid.NewGuid():N}@droga.co";

        await RegisterUserAsync(tenantId, email);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "la-que-no-es", deviceId = "dev-1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("errorCode").GetString().Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithAnUnknownEmail_Returns401Identically()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = $"fantasma-{Guid.NewGuid():N}@droga.co", password = "Contrasena-Larga-1", deviceId = "dev-1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("errorCode").GetString().Should().Be(
            "AUTH_INVALID_CREDENTIALS",
            "T055c: un email inexistente y una contraseña incorrecta deben ser indistinguibles");
    }

    [Fact]
    public async Task Login_FromAnUnknownDevice_RequiresDeviceConfirmationAndIssuesNoToken()
    {
        var tenantId = await SeedTenantAsync();
        var email = $"nuevo-{Guid.NewGuid():N}@droga.co";

        await RegisterUserAsync(tenantId, email);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Contrasena-Larga-1", deviceId = "dev-desconocido" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("requiresDeviceConfirmation").GetBoolean().Should().BeTrue();
        body.GetProperty("accessToken").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Login_WithoutTheTenantHeader_IsReachable()
    {
        var tenantId = await SeedTenantAsync();
        var email = $"sin-header-{Guid.NewGuid():N}@droga.co";

        await RegisterUserAsync(tenantId, email);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Contrasena-Larga-1", deviceId = "dev-desconocido" });

        response.StatusCode.Should().NotBe(
            HttpStatusCode.BadRequest,
            "T055a: el login está exento del TenantMiddleware y no debe exigir X-Tenant-ID");
    }

    [Fact]
    public async Task Auth_BeyondTheRateLimit_Returns429()
    {
        using var limited = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Auth:PermitPerWindow", "2"));

        var client = limited.CreateClient();
        var payload = new
        {
            email = $"flood-{Guid.NewGuid():N}@droga.co",
            password = "Contrasena-Larga-1",
            deviceId = "dev-1",
        };

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", payload);
            statuses.Add(response.StatusCode);
        }

        statuses.Take(2).Should().AllBeEquivalentTo(
            HttpStatusCode.Unauthorized,
            "los intentos dentro del límite se responden por credenciales, no por cuota");
        statuses[2].Should().Be(
            HttpStatusCode.TooManyRequests,
            "NFR-005: pasado el límite por IP la superficie de auth debe cortar");
    }
}
