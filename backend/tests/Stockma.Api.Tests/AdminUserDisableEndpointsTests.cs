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

// T068 — offboarding real: deshabilitar corta login, sesión viva y dispositivos en un solo acto.
public class AdminUserDisableEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string TenantHeader = "X-Tenant-ID";
    private const string DeviceId = "web-chrome-a91f2c77";
    private const string Password = "Contrasena-Larga-1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Disable_AsAnAdmin_CutsTheSessionTheDeviceAndTheLogin()
    {
        var tenantId = await SeedTenantAsync();
        var admin = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedLiveSessionAsync(tenantId, member.Id);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, admin.Id)
            .PostAsync($"/api/admin/users/{member.Id}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("userId").GetGuid().Should().Be(member.Id);
        body.GetProperty("enabled").GetBoolean().Should().BeFalse();

        (await AnyLiveSessionAsync(tenantId, member.Id))
            .Should().BeFalse("deshabilitar revoca todas las familias de refresh en el mismo acto");
        (await AnyActiveDeviceAsync(tenantId, member.Id))
            .Should().BeFalse("y también sus dispositivos confiables: un offboarding a medias no es un offboarding");

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { member.Email, password = Password, deviceId = DeviceId });

        login.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "el deshabilitado no entra ni siquiera desde el dispositivo trusted que tenía sembrado");
        (await ErrorCodeAsync(login)).Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Enable_AfterADisable_LogsInAgainButTheOldSessionStaysDead()
    {
        var tenantId = await SeedTenantAsync();
        var admin = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedLiveSessionAsync(tenantId, member.Id);

        var adminClient = CreateClient(tenantId, TenantRoles.TenantAdmin, admin.Id);
        (await adminClient.PostAsync($"/api/admin/users/{member.Id}/disable", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/admin/users/{member.Id}/enable", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { member.Email, password = Password, deviceId = DeviceId });

        login.StatusCode.Should().Be(HttpStatusCode.OK, "rehabilitar reabre el acceso");

        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        loginBody.GetProperty("requiresDeviceConfirmation").GetBoolean().Should()
            .BeTrue("los dispositivos viejos quedaron revocados: el 2FA vuelve desde cero");

        (await AnyLiveSessionAsync(tenantId, member.Id))
            .Should().BeFalse("rehabilitar no resucita la sesión vieja: las familias murieron en el disable");
    }

    [Fact]
    public async Task Disable_TheLastAdmin_Returns400AndKeepsHimAbleToLogin()
    {
        var tenantId = await SeedTenantAsync();
        var admin = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, admin.Id)
            .PostAsync($"/api/admin/users/{admin.Id}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { admin.Email, password = Password, deviceId = DeviceId });

        login.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "el rechazo no tocó nada: el único admin del tenant sigue pudiendo entrar");
    }

    [Fact]
    public async Task Disable_AsAMember_Returns403()
    {
        var tenantId = await SeedTenantAsync();
        var caller = await SeedUserAsync(tenantId, TenantRoles.Member);
        var target = await SeedUserAsync(tenantId, TenantRoles.Member);

        var response = await CreateClient(tenantId, TenantRoles.Member, caller.Id)
            .PostAsync($"/api/admin/users/{target.Id}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "T068: sólo un TenantAdmin toca cuentas");
    }

    [Fact]
    public async Task Disable_ForAUserOfAnotherTenant_Returns404()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();
        var adminA = await SeedUserAsync(tenantA, TenantRoles.TenantAdmin);
        var userB = await SeedUserAsync(tenantB, TenantRoles.Member);

        var response = await CreateClient(tenantA, TenantRoles.TenantAdmin, adminA.Id)
            .PostAsync($"/api/admin/users/{userB.Id}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "T068: cuentas aisladas por tenant (FR-002)");
        (await ErrorCodeAsync(response)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Disable_WithoutAToken_Returns401()
    {
        var tenantId = await SeedTenantAsync();
        var target = await SeedUserAsync(tenantId, TenantRoles.Member);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());

        var response = await client.PostAsync($"/api/admin/users/{target.Id}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DisabledUser_CannotRequestNorConfirmAPhoneNumberChange()
    {
        var tenantId = await SeedTenantAsync();
        var admin = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);

        var adminClient = CreateClient(tenantId, TenantRoles.TenantAdmin, admin.Id);
        (await adminClient.PostAsync($"/api/admin/users/{member.Id}/disable", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // JWT del miembro minted ANTES del disable: sigue siendo firmemente válido pero la cuenta no.
        var staleClient = factory.CreateAuthenticatedClient(tenantId, tenantId, TenantRoles.Member, member.Id);

        var request = await staleClient.PutAsJsonAsync(
            "/api/auth/phone-number",
            new { newPhoneNumber = "+573001234567" });

        request.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "T068: el deshabilitado no puede pedir cambio de número ni con un JWT viejo");
        (await ErrorCodeAsync(request)).Should().Be("AUTH_ACCOUNT_DISABLED");

        var confirm = await staleClient.PostAsJsonAsync(
            "/api/auth/phone-number/confirm",
            new { otp = "123456" });

        confirm.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(confirm)).Should().Be(
            "AUTH_ACCOUNT_DISABLED",
            "el chequeo corre antes de gastar el OTP: el offboarding corta también el cambio en vuelo");
    }

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccountWithTheSameResponseAsAnyWrongPassword()
    {
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = factory.CreateClient();

        HttpResponseMessage lastWrong = null!;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            lastWrong = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { member.Email, password = $"Errada-{attempt}#2026Larga", deviceId = DeviceId });

            lastWrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var withTheRightPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { member.Email, password = Password, deviceId = DeviceId });

        withTheRightPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "5 fallos bloquean la cuenta");
        (await withTheRightPassword.Content.ReadAsStringAsync()).Should()
            .Be(
                await lastWrong.Content.ReadAsStringAsync(),
                "bloqueado y contraseña errada responden byte a byte igual: el bloqueo no se puede delatar");
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

    private async Task<(Guid Id, string Email)> SeedUserAsync(Guid tenantId, string role)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
        var email = $"{role}-{Guid.NewGuid():N}@droga.co";

        var userId = await accounts.CreateAsync(new NewUser(
            tenantId, email, Password, "+573001234567", role));

        return (userId, email);
    }

    private HttpClient CreateClient(Guid tenantId, string role, Guid userId) =>
        factory.CreateAuthenticatedClient(tenantId, tenantId, role, userId);

    private async Task SeedLiveSessionAsync(Guid tenantId, Guid userId)
    {
        var stampedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var uniqueHash = $"t068-{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, last_used_at, expires_at)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, {DeviceId}, 'fp-t068',
                    {stampedAt}, {stampedAt}, {stampedAt.AddDays(15)});

            INSERT INTO refresh_tokens (id, tenant_id, user_id, family_id, device_id, token_hash,
                                        issued_at, expires_at, family_expires_at)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, gen_random_uuid(), {DeviceId},
                    {uniqueHash}, {stampedAt}, {stampedAt.AddMinutes(30)}, {stampedAt.AddHours(8)});
            """);
    }

    private async Task<bool> AnyLiveSessionAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        return await context.RefreshTokens.AnyAsync(token => token.UserId == userId && token.RevokedAt == null);
    }

    private async Task<bool> AnyActiveDeviceAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();
        var now = DateTimeOffset.UtcNow;

        return await context.TrustedDevices.AnyAsync(
            device => device.UserId == userId && device.RevokedAt == null && device.ExpiresAt > now);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;
}
