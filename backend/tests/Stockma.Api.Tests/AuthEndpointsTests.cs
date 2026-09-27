using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class AuthEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string TenantHeader = "X-Tenant-ID";
    private const string DeviceId = "web-chrome-a91f2c77";
    private const string ProblemJson = "application/problem+json";

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

    private async Task<Guid> SeedAdminAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();

        return await accounts.CreateAsync(new NewUser(
            tenantId,
            $"admin-{Guid.NewGuid():N}@droga.co",
            "Contrasena-Larga-1",
            "+5491100000099",
            TenantRoles.TenantAdmin));
    }

    private HttpClient CreateAuthorizedClient(Guid headerTenantId, Guid tokenTenantId, Guid userId, string role) =>
        factory.CreateAuthenticatedClient(headerTenantId, tokenTenantId, role, userId);

    private HttpClient CreateAdminClient(Guid tenantId, Guid adminUserId) =>
        CreateAuthorizedClient(tenantId, tenantId, adminUserId, TenantRoles.TenantAdmin);

    private static object RegisterBody(string email) => new
    {
        email,
        password = "Contrasena-Larga-1",
        phoneNumber = "+5491100000000",
    };

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.TryGetProperty("errorCode", out var errorCode) ? errorCode.GetString() : null;
    }

    private async Task<string> RegisterUserAsync(Guid tenantId, string email)
    {
        var adminId = await SeedAdminAsync(tenantId);

        var response = await CreateAdminClient(tenantId, adminId)
            .PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return email;
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_Returns409WithTheErrorCode()
    {
        var tenantId = await SeedTenantAsync();
        var email = $"dup-{Guid.NewGuid():N}@droga.co";

        await RegisterUserAsync(tenantId, email);

        var adminId = await SeedAdminAsync(tenantId);
        var response = await CreateAdminClient(tenantId, adminId)
            .PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("errorCode").GetString().Should().Be("AUTH_EMAIL_DUPLICATE");
    }

    [Fact]
    public async Task Register_WithoutAToken_Returns401()
    {
        var tenantId = await SeedTenantAsync();

        var response = await CreateTenantClient(tenantId)
            .PostAsJsonAsync("/api/auth/register", RegisterBody($"anon-{Guid.NewGuid():N}@droga.co"));

        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "T064: sin JWT, register no puede ser alcanzado por cualquiera que conozca el tenant");
    }

    [Fact]
    public async Task Register_WithAMemberToken_Returns403()
    {
        var tenantId = await SeedTenantAsync();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();

        var memberId = await accounts.CreateAsync(new NewUser(
            tenantId,
            $"member-{Guid.NewGuid():N}@droga.co",
            "Contrasena-Larga-1",
            "+5491100000098",
            TenantRoles.Member));

        var client = CreateAuthorizedClient(tenantId, tenantId, memberId, TenantRoles.Member);

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", RegisterBody($"nuevo-{Guid.NewGuid():N}@droga.co"));

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "T064: un Member puede leer/ajustar stock pero no dar de alta usuarios");
    }

    [Fact]
    public async Task Register_WithAnAdminTokenFromAnotherTenant_Returns403AndCreatesNoUser()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();
        var adminOfA = await SeedAdminAsync(tenantA);

        var client = CreateAuthorizedClient(tenantB, tenantA, adminOfA, TenantRoles.TenantAdmin);
        var email = $"cross-{Guid.NewGuid():N}@droga.co";

        var response = await client.PostAsJsonAsync("/api/auth/register", RegisterBody(email));

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "T064: un TenantAdmin de OTRO tenant no puede tomar el tenant del header");
        (await ReadErrorCodeAsync(response)).Should().Be(
            "TENANT_MISMATCH",
            "T073: el rechazo ahora lo hace el TenantMiddleware general, no un chequeo ad hoc del controller");

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantB);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        (await context.Users.AnyAsync(user => user.Email == email)).Should().BeFalse(
            "un intento cross-tenant rechazado no debe dejar ningún usuario creado");
    }

    [Fact]
    public async Task Register_WithAnAdminToken_Returns201()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedAdminAsync(tenantId);

        var response = await CreateAdminClient(tenantId, adminId)
            .PostAsJsonAsync("/api/auth/register", RegisterBody($"ok-{Guid.NewGuid():N}@droga.co"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401WithTheUniformErrorCode()
    {
        var tenantId = await SeedTenantAsync();
        var email = $"wrong-{Guid.NewGuid():N}@droga.co";

        await RegisterUserAsync(tenantId, email);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "la-que-no-es", deviceId = DeviceId });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("errorCode").GetString().Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithAnUnknownEmail_Returns401Identically()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = $"fantasma-{Guid.NewGuid():N}@droga.co", password = "Contrasena-Larga-1", deviceId = DeviceId });

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
            new { email, password = "Contrasena-Larga-1", deviceId = DeviceId });

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
            new { email, password = "Contrasena-Larga-1", deviceId = DeviceId });

        response.StatusCode.Should().NotBe(
            HttpStatusCode.BadRequest,
            "T055a: el login está exento del TenantMiddleware y no debe exigir X-Tenant-ID");
    }

    [Fact]
    public async Task ConfirmDevice_WithoutAuthentication_ReachesTheHandler()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/confirm-device",
            new { email = $"anon-{Guid.NewGuid():N}@droga.co", deviceId = DeviceId, fingerprint = "fp-1", otp = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(response)).Should().Be(
            "AUTH_OTP_REJECTED",
            "T073: confirm-device sigue siendo [AllowAnonymous]; si el FallbackPolicy lo bloqueara "
            + "la request nunca llegaria al handler y no habria errorCode de negocio en el cuerpo");
    }

    [Fact]
    public async Task ConfirmDevice_ForAnUnknownEmailAndForAWrongCode_AnswersIdentically()
    {
        var tenantId = await SeedTenantAsync();
        var email = await RegisterUserAsync(tenantId, $"otp-{Guid.NewGuid():N}@droga.co");
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Contrasena-Larga-1", deviceId = DeviceId });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var wrongCode = await client.PostAsJsonAsync(
            "/api/auth/confirm-device",
            new { email, deviceId = DeviceId, fingerprint = "fp-1", otp = "000000" });
        var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/confirm-device",
            new { email = $"fantasma-{Guid.NewGuid():N}@droga.co", deviceId = DeviceId, fingerprint = "fp-1", otp = "000000" });

        wrongCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await wrongCode.Content.ReadAsStringAsync())
            .Should()
            .Be(
                await unknownEmail.Content.ReadAsStringAsync(),
                "ADR-016: toda falla de confirm-device es el mismo 401 AUTH_OTP_REJECTED; si no, enumera correos");
    }

    [Fact]
    public async Task ConfirmDevice_WithoutAFingerprint_Returns400ValidationFailed()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/confirm-device",
            new { email = $"anon-{Guid.NewGuid():N}@droga.co", deviceId = DeviceId, otp = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        (await ReadErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Login_WithoutADeviceId_Returns400ValidationFailed()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = $"sin-device-{Guid.NewGuid():N}@droga.co", password = "Contrasena-Larga-1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "auth-api.md: deviceId obligatorio, 400 y no 500");
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        (await ReadErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Login_WithALowEntropyDeviceId_Returns400ValidationFailed()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = $"corto-{Guid.NewGuid():N}@droga.co", password = "Contrasena-Larga-1", deviceId = "dev-1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorCodeAsync(response)).Should().Be(
            "VALIDATION_FAILED",
            "ADR-016: la seguridad del dispositivo descansa en la entropía del deviceId");
    }

    [Fact]
    public async Task Register_WithAPasswordIdentityRejects_Returns400ValidationFailed()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedAdminAsync(tenantId);

        var response = await CreateAdminClient(tenantId, adminId).PostAsJsonAsync(
            "/api/auth/register",
            new { email = $"debil-{Guid.NewGuid():N}@droga.co", password = "corta", phoneNumber = "+5491100000000" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "auth-api.md: 400 VALIDATION_FAILED, no un 500");
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        (await ReadErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Auth_BeyondTheRateLimit_AnswersProblemJsonWithTheErrorCode()
    {
        using var limited = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Auth:PermitPerWindow", "1"));

        var client = limited.CreateClient();
        var payload = new { email = $"flood-{Guid.NewGuid():N}@droga.co", password = "Contrasena-Larga-1", deviceId = DeviceId };

        await client.PostAsJsonAsync("/api/auth/login", payload);
        var rejected = await client.PostAsJsonAsync("/api/auth/login", payload);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        (await ReadErrorCodeAsync(rejected)).Should().Be("AUTH_RATE_LIMITED", "auth-api.md: 429 AUTH_RATE_LIMITED");
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
            deviceId = DeviceId,
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
