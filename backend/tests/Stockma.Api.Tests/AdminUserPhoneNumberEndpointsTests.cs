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

public class AdminUserPhoneNumberEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string TenantHeader = "X-Tenant-ID";
    private const string ValidPhone = "+573001234567";
    private const string DeviceId = "web-chrome-a91f2c77";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SetPhoneNumber_AsAnAdmin_ReturnsTheMaskedNumberAndPersistsIt()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PutAsJsonAsync($"/api/admin/users/{targetId}/phone-number", new { phoneNumber = ValidPhone });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("userId").GetGuid().Should().Be(targetId);
        body.GetProperty("phoneNumberMasked").GetString()
            .Should().Be("+57300*****67", "auth-api.md fija phoneNumberMasked");
        body.GetProperty("phoneNumberConfirmed").GetBoolean().Should().BeTrue();

        (await GetStoredPhoneAsync(tenantId, targetId)).Should()
            .Be((ValidPhone, true), "el número cargado por el admin es el que recibe el OTP");
    }

    [Fact]
    public async Task SetPhoneNumber_CutsEverySessionAndTrustedDeviceOfThatUser()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedLiveSessionAsync(tenantId, targetId);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PutAsJsonAsync($"/api/admin/users/{targetId}/phone-number", new { phoneNumber = ValidPhone });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await AnyLiveSessionAsync(tenantId, targetId)).Should()
            .BeFalse("T050: cambiar el número debe cerrar la sesión de inmediato");
        (await IsDeviceTrustedAsync(tenantId, targetId, DeviceId)).Should()
            .BeFalse("un dispositivo confiable saltea el OTP: volver a entrar exige el código al número nuevo");
    }

    [Fact]
    public async Task SetPhoneNumber_WithAnInvalidNumber_Returns400AndLeavesTheSessionAlive()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedLiveSessionAsync(tenantId, targetId);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PutAsJsonAsync($"/api/admin/users/{targetId}/phone-number", new { phoneNumber = "3001234567" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
        (await AnyLiveSessionAsync(tenantId, targetId)).Should()
            .BeTrue("un número mal formado no puede echar al usuario: la validación va antes de todo");
    }

    [Fact]
    public async Task SetPhoneNumber_AsAMember_Returns403()
    {
        var tenantId = await SeedTenantAsync();
        var memberId = await SeedUserAsync(tenantId, TenantRoles.Member);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);

        var response = await CreateClient(tenantId, TenantRoles.Member, memberId)
            .PutAsJsonAsync($"/api/admin/users/{targetId}/phone-number", new { phoneNumber = ValidPhone });

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "T050: sólo el admin del tenant puede cargar o cambiar números");
    }

    [Fact]
    public async Task SetPhoneNumber_ForAnUnknownUser_Returns404()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PutAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/phone-number", new { phoneNumber = ValidPhone });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task SetPhoneNumber_ForAUserOfAnotherTenant_Returns404()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();
        var adminA = await SeedUserAsync(tenantA, TenantRoles.TenantAdmin);
        var userB = await SeedUserAsync(tenantB, TenantRoles.Member);

        var response = await CreateClient(tenantA, TenantRoles.TenantAdmin, adminA)
            .PutAsJsonAsync($"/api/admin/users/{userB}/phone-number", new { phoneNumber = ValidPhone });

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "un admin de un tenant no puede tocar usuarios de otro (FR-002)");
        (await ErrorCodeAsync(response)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task SetPhoneNumber_WithoutAToken_Returns401()
    {
        var tenantId = await SeedTenantAsync();
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{targetId}/phone-number",
            new { phoneNumber = ValidPhone });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IdentitySelfServiceSurface_IsNotMapped()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);

        // Con fallback de autenticación global, un anónimo recibiría 401 en cualquier path:
        // el sentinel necesita un cliente autenticado para poder distinguir 404 de 200.
        var client = CreateClient(tenantId, TenantRoles.TenantAdmin, adminId);

        var userInfo = await client.GetAsync("/userinfo");
        var register = await client.PostAsync("/register", JsonContent.Create(new { }));

        userInfo.StatusCode.Should().Be(HttpStatusCode.NotFound, "T050: Identity API no expone /userinfo");
        register.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "T050: el registro propio vive en /api/auth/register y exige admin; /register de Identity queda cerrado");
    }

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

    private async Task<Guid> SeedUserAsync(Guid tenantId, string role)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();

        return await accounts.CreateAsync(new NewUser(
            tenantId,
            $"{role}-{Guid.NewGuid():N}@droga.co",
            "Contrasena-Larga-1",
            "+5491100000097",
            role));
    }

    private HttpClient CreateClient(Guid tenantId, string role, Guid userId) =>
        factory.CreateAuthenticatedClient(tenantId, tenantId, role, userId);

    private async Task SeedLiveSessionAsync(Guid tenantId, Guid userId)
    {
        var stampedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var uniqueHash = $"t050-{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, last_used_at, expires_at)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, {DeviceId}, 'fp-t050',
                    {stampedAt}, {stampedAt}, {stampedAt.AddDays(15)});

            INSERT INTO refresh_tokens (id, tenant_id, user_id, family_id, device_id, token_hash,
                                        issued_at, expires_at, family_expires_at)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, gen_random_uuid(), {DeviceId},
                    {uniqueHash}, {stampedAt}, {stampedAt.AddMinutes(120)}, {stampedAt.AddHours(8)});
            """);
    }

    private async Task<bool> AnyLiveSessionAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        return await context.RefreshTokens.AnyAsync(token => token.UserId == userId && token.RevokedAt == null);
    }

    private async Task<bool> IsDeviceTrustedAsync(Guid tenantId, Guid userId, string deviceId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var devices = scope.ServiceProvider.GetRequiredService<ITrustedDevices>();

        return await devices.IsTrustedAsync(userId, deviceId);
    }

    private async Task<(string? PhoneNumber, bool Confirmed)> GetStoredPhoneAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        var user = await context.Users.SingleAsync(u => u.Id == userId);

        return (user.PhoneNumber, user.PhoneNumberConfirmed);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;
}
