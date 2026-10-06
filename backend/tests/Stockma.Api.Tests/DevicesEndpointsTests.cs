using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Queries;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class DevicesEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
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

    private async Task SeedTrustedDeviceAsync(Guid tenantId, Guid userId, string deviceId, string tokenHash)
    {
        var stampedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var uniqueHash = $"{tokenHash}-{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, last_used_at, expires_at)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, {deviceId}, 'fp',
                    {stampedAt}, {stampedAt}, {stampedAt.AddDays(15)});

            INSERT INTO refresh_tokens (id, tenant_id, user_id, family_id, device_id, token_hash,
                                        issued_at, expires_at, family_expires_at)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, gen_random_uuid(), {deviceId}, {uniqueHash},
                    {stampedAt}, {stampedAt.AddMinutes(30)}, {stampedAt.AddHours(8)});
            """);
    }

    private async Task<bool> AnyLiveSessionAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        return await context.RefreshTokens.AnyAsync(token => token.UserId == userId && token.RevokedAt == null);
    }

    private async Task<Guid> GetTrustedDeviceIdAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        return (await context.TrustedDevices.SingleAsync(device => device.UserId == userId)).Id;
    }

    [Fact]
    public async Task GetMyDevices_ReturnsOnlyTheCallersTrustedDevices()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, TenantRoles.Member);
        var otherUserId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedTrustedDeviceAsync(tenantId, userId, "web-chrome-a91f2c77", "hash-propio");
        await SeedTrustedDeviceAsync(tenantId, otherUserId, "web-firefox-11223344", "hash-ajeno");

        var response = await CreateClient(tenantId, TenantRoles.Member, userId)
            .GetAsync("/api/auth/devices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var devices = await response.Content.ReadFromJsonAsync<List<TrustedDeviceInfo>>(JsonOptions);

        devices.Should().ContainSingle(device => device.DeviceId == "web-chrome-a91f2c77" && device.IsActive);
        devices.Should().NotContain(device => device.DeviceId == "web-firefox-11223344",
            "cada uno ve los suyos: es como el dueño detecta un dispositivo que no reconoce");
    }

    [Fact]
    public async Task GetMyDevices_ListsTheLastUseOfEachDevice()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedTrustedDeviceAsync(tenantId, userId, "web-chrome-a91f2c77", "hash-propio");

        var response = await CreateClient(tenantId, TenantRoles.Member, userId)
            .GetAsync("/api/auth/devices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var devices = await response.Content.ReadFromJsonAsync<List<TrustedDeviceInfo>>(JsonOptions);

        var device = devices.Should()
            .ContainSingle(candidate => candidate.DeviceId == "web-chrome-a91f2c77")
            .Subject;

        device.LastUsedAt
            .Should()
            .BeCloseTo(
                device.TrustedAt,
                TimeSpan.FromSeconds(1),
                "T067: el alta ya es un uso, así que sin registro previo el piso del último uso es trusted_at");
    }

    [Fact]
    public async Task GetMyDevices_WithoutAToken_Returns401()
    {
        var tenantId = await SeedTenantAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());

        var response = await client.GetAsync("/api/auth/devices");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserDevices_WithAMemberToken_Returns403()
    {
        var tenantId = await SeedTenantAsync();
        var memberId = await SeedUserAsync(tenantId, TenantRoles.Member);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedTrustedDeviceAsync(tenantId, targetId, "web-chrome-a91f2c77", "hash-ajeno");

        var response = await CreateClient(tenantId, TenantRoles.Member, memberId)
            .GetAsync($"/api/admin/users/{targetId}/devices");

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "T067: un Member NO DEBE poder ver los dispositivos de otro usuario");
    }

    [Fact]
    public async Task RevokeMyDevice_Returns204AndKillsTheLiveSession()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedTrustedDeviceAsync(tenantId, userId, "web-chrome-a91f2c77", "hash-propio");
        var trustedDeviceId = await GetTrustedDeviceIdAsync(tenantId, userId);

        var response = await CreateClient(tenantId, TenantRoles.Member, userId)
            .PostAsync($"/api/auth/devices/{trustedDeviceId}/revoke", null);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);
        (await AnyLiveSessionAsync(tenantId, userId)).Should()
            .BeFalse("tasks.md T067: revocar sin cerrar la sesión es teatro");
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;

    [Fact]
    public async Task RevokeMyDevice_ForAnUnknownDevice_Returns404()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, TenantRoles.Member);

        var response = await CreateClient(tenantId, TenantRoles.Member, userId)
            .PostAsync($"/api/auth/devices/{Guid.NewGuid()}/revoke", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("DEVICE_NOT_FOUND");
    }

    [Fact]
    public async Task RevokeMyDevice_Twice_Returns204BothTimes()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedTrustedDeviceAsync(tenantId, userId, "web-chrome-a91f2c77", "hash-propio");
        var trustedDeviceId = await GetTrustedDeviceIdAsync(tenantId, userId);
        var client = CreateClient(tenantId, TenantRoles.Member, userId);

        var first = await client.PostAsync($"/api/auth/devices/{trustedDeviceId}/revoke", null);
        var second = await client.PostAsync($"/api/auth/devices/{trustedDeviceId}/revoke", null);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent, "revocar es idempotente");
    }

    [Fact]
    public async Task RevokeAllAsAnAdmin_Returns204AndLeavesNoLiveSession()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedTrustedDeviceAsync(tenantId, targetId, "web-chrome-a91f2c77", "hash-uno");
        await SeedTrustedDeviceAsync(tenantId, targetId, "web-firefox-11223344", "hash-dos");

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PostAsync($"/api/admin/users/{targetId}/devices/revoke-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AnyLiveSessionAsync(tenantId, targetId)).Should()
            .BeFalse("revoke-all no puede dejar al usuario con sesiones abiertas");
    }

    [Fact]
    public async Task GetUserDevices_ForAnUnknownUser_Returns404()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .GetAsync($"/api/admin/users/{Guid.NewGuid()}/devices");

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "un userId mal tipeado no puede parecer un usuario sin dispositivos");
        (await ErrorCodeAsync(response)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task RevokeAllAsAnAdmin_ForAUserWithoutDevices_Returns204Not404()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PostAsync($"/api/admin/users/{targetId}/devices/revoke-all", null);

        response.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "sin dispositivos que revocar sigue siendo idempotente: no existe es otra cosa");
    }

    [Fact]
    public async Task RevokeAllAsAnAdmin_ForAnUnknownUser_Returns404()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PostAsync($"/api/admin/users/{Guid.NewGuid()}/devices/revoke-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task RevokeAsAnAdmin_TheDeviceOfAnotherUser_Returns404AndKeepsIt()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var ownerId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedTrustedDeviceAsync(tenantId, ownerId, "web-chrome-a91f2c77", "hash-propio");
        var ownedDeviceId = await GetTrustedDeviceIdAsync(tenantId, ownerId);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PostAsync($"/api/admin/users/{Guid.NewGuid()}/devices/{ownedDeviceId}/revoke", null);

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "el dispositivo pertenece a otro usuario: para ese userId no existe");
        (await ErrorCodeAsync(response)).Should().Be("DEVICE_NOT_FOUND");

        var stillTrusted = await IsDeviceLiveAsync(tenantId, ownerId);
        stillTrusted.Should().BeTrue("un 404 no puede revocarle la sesión a nadie");
    }

    private async Task<bool> IsDeviceLiveAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        return await context.TrustedDevices.AnyAsync(device => device.UserId == userId && device.RevokedAt == null);
    }
}
