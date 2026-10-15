using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class PasswordResetEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string DeviceId = "web-chrome-a91f2c77";
    private const string Fingerprint = "fp-mostrador";
    private const string OtpCode = "483920";
    private const string Password = "Contrasena-Larga-1";
    private const string NewPassword = "Nueva-Recuperada-1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class FixedOtpGenerator : IOtpGenerator
    {
        public string Generate() => OtpCode;
    }

    private WebApplicationFactory<Program> WithKnownOtp() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IOtpGenerator, FixedOtpGenerator>())));


    [Fact]
    public async Task Forgot_ForAnExistingAndForAnUnknownEmail_AnswerByteForByteTheSame()
    {
        using var app = WithKnownOtp();
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = app.CreateClient();

        var existing = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = member.Email });
        var unknown = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = $"nadie-{Guid.NewGuid():N}@droga.co" });

        existing.StatusCode.Should().Be(HttpStatusCode.Accepted);
        unknown.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await existing.Content.ReadAsStringAsync()).Should().Be(
            await unknown.Content.ReadAsStringAsync(),
            "T069: distinguir permitiría enumerar qué correos usan Stockma (mismo motivo que T055c)");

        (await OtpCountAsync(tenantId, member.Id))
            .Should().Be(1, "el código sólo se emite para la cuenta que sí existe");
    }

    [Fact]
    public async Task Forgot_WithoutAnEnrolledPhone_AnswersTheSameAndEmitsNothing()
    {
        using var app = WithKnownOtp();
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member, phoneNumber: null);
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = member.Email });

        response.StatusCode.Should().Be(
            HttpStatusCode.Accepted,
            "sin número no hay canal SMS: ese caso lo resuelve el admin, pero la respuesta pública no cambia");
        (await OtpCountAsync(tenantId, member.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Forgot_AfterBeingDisabled_DoesNotStartTheFlow()
    {
        using var app = WithKnownOtp();
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SetEnabledAsync(tenantId, member.Id, enabled: false);
        var client = app.CreateClient();

        var disabled = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = member.Email });
        var unknown = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = $"nadie-{Guid.NewGuid():N}@droga.co" });

        disabled.StatusCode.Should().Be(HttpStatusCode.Accepted, "el deshabilitado no se delata con un 403");
        (await disabled.Content.ReadAsStringAsync())
            .Should().Be(await unknown.Content.ReadAsStringAsync());
        (await OtpCountAsync(tenantId, member.Id))
            .Should().Be(0, "FindByByEmail es lockout-aware: una cuenta bloqueada no emite código (T068)");
    }

    [Fact]
    public async Task Reset_WithAWrongCode_AndForAnUnknownEmail_ReturnTheSame401()
    {
        using var app = WithKnownOtp();
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = app.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = member.Email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var wrongCode = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = member.Email, code = "000000", newPassword = NewPassword });
        var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = $"nadie-{Guid.NewGuid():N}@droga.co", code = "000000", newPassword = NewPassword });

        wrongCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(wrongCode)).Should().Be("AUTH_OTP_REJECTED");
        (await wrongCode.Content.ReadAsStringAsync()).Should().Be(
            await unknownEmail.Content.ReadAsStringAsync(),
            "código erróneo e email inexistente responden idéntico: el paso 2 tampoco enumera");

        var good = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = member.Email, code = OtpCode, newPassword = NewPassword });

        good.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "un intento fallido con código malo no consume el código vigente");
    }

    [Fact]
    public async Task Reset_WithTheCorrectCode_ChangesThePasswordAndKillsTheLiveSession()
    {
        using var app = WithKnownOtp();
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = app.CreateClient();
        var cookie = await ArrangeLiveSessionAsync(app, member.Email);

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = member.Email }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = member.Email, code = OtpCode, newPassword = NewPassword });

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent, await reset.Content.ReadAsStringAsync());

        var refresh = await RefreshAsync(client, cookie);

        refresh.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "T065: un reset que no corta sesiones no echa al atacante que ya estaba adentro");
    }

    [Fact]
    public async Task Reset_UsesTheCodeOnlyOnce()
    {
        using var app = WithKnownOtp();
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = app.CreateClient();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = member.Email });

        var first = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = member.Email, code = OtpCode, newPassword = NewPassword });
        var second = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = member.Email, code = OtpCode, newPassword = "Otra-Recuperada-2" });

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "T069: el código de reset es de un solo uso");
        (await ErrorCodeAsync(second)).Should().Be("AUTH_OTP_REJECTED");
    }

    [Fact]
    public async Task Reset_WithAWeakNewPassword_Returns400AndKeepsTheCodeUsable()
    {
        using var app = WithKnownOtp();
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = app.CreateClient();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = member.Email });

        var weak = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = member.Email, code = OtpCode, newPassword = "123" });

        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(weak)).Should().Be("VALIDATION_FAILED");

        var retry = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = member.Email, code = OtpCode, newPassword = NewPassword });

        retry.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "la política se evalúa dentro de la transacción del consume: si falla, el código NO se quema");
    }

    [Fact]
    public async Task AfterAReset_TheOldPasswordFailsAndANewDeviceStillNeedsTheOtp()
    {
        using var app = WithKnownOtp();
        var tenantId = await SeedTenantAsync();
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = app.CreateClient();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = member.Email });
        (await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email = member.Email, code = OtpCode, newPassword = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var oldPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { member.Email, password = Password, deviceId = "web-chrome-nuevo-1" });

        oldPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "la contraseña vieja muere con el reset");

        var newPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { member.Email, password = NewPassword, deviceId = "web-chrome-nuevo-2" });

        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginBody = await newPasswordLogin.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        loginBody.GetProperty("requiresDeviceConfirmation").GetBoolean().Should().BeTrue(
            "T069 no salta el 2FA: la contraseña nueva no alcanza, el canal OTP sigue mandando");
    }

    [Fact]
    public async Task AdminReset_AsTenantAdmin_RevokesSessionsAndOpensTheWayBack()
    {
        var tenantId = await SeedTenantAsync();
        var admin = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedLiveSessionAsync(tenantId, member.Id);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, admin.Id)
            .PostAsJsonAsync($"/api/admin/users/{member.Id}/reset-password", new { newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("userId").GetGuid().Should().Be(member.Id);

        (await AnyLiveSessionAsync(tenantId, member.Id))
            .Should().BeFalse("cambiar la contraseña sin cortar la sesión no echa al atacante que ya estaba adentro");

        (await AnyActiveDeviceAsync(tenantId, member.Id)).Should().BeTrue(
            "decisión T069: el reset NO toca los dispositivos confiables — el ancla OTP (el número) no cambió, "
            + "y sin la contraseña nueva un dispositivo no entra. Flaggeado para veto en review.");

        var client = factory.CreateClient();

        var oldPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { member.Email, password = Password, deviceId = DeviceId });

        oldPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { member.Email, password = NewPassword, deviceId = DeviceId });

        login.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "con la contraseña nueva y el dispositivo que ya era confiable, la cuenta vuelve a ser accesible");
    }

    [Fact]
    public async Task AdminReset_AsAMember_Returns403()
    {
        var tenantId = await SeedTenantAsync();
        var caller = await SeedUserAsync(tenantId, TenantRoles.Member);
        var target = await SeedUserAsync(tenantId, TenantRoles.Member);

        var response = await CreateClient(tenantId, TenantRoles.Member, caller.Id)
            .PostAsJsonAsync($"/api/admin/users/{target.Id}/reset-password", new { newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "sólo un TenantAdmin toca contraseñas");
    }

    [Fact]
    public async Task AdminReset_ForAUserOfAnotherTenant_Returns404()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();
        var adminA = await SeedUserAsync(tenantA, TenantRoles.TenantAdmin);
        var userB = await SeedUserAsync(tenantB, TenantRoles.Member);

        var response = await CreateClient(tenantA, TenantRoles.TenantAdmin, adminA.Id)
            .PostAsJsonAsync($"/api/admin/users/{userB.Id}/reset-password", new { newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "cuentas aisladas por tenant (FR-002)");
        (await ErrorCodeAsync(response)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task AdminReset_WithAWeakPassword_Returns400AndTheSessionsStayCut()
    {
        var tenantId = await SeedTenantAsync();
        var admin = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var member = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedLiveSessionAsync(tenantId, member.Id);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, admin.Id)
            .PostAsJsonAsync($"/api/admin/users/{member.Id}/reset-password", new { newPassword = "123" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");

        (await AnyLiveSessionAsync(tenantId, member.Id)).Should().BeFalse(
            "decisión T069: fail-closed al estilo T061/T068 — primero se corta el acceso, "
            + "aunque la contraseña después falle por política. Flaggeado para veto en review.");

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { member.Email, password = Password, deviceId = DeviceId });

        login.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "la política rechazó la contraseña: la anterior sigue intacta y el miembro puede volver a entrar");
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

    private async Task<(Guid Id, string Email)> SeedUserAsync(
        Guid tenantId,
        string role,
        string? phoneNumber = "+573001234567")
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
        var email = $"{role}-{Guid.NewGuid():N}@droga.co";

        var userId = await accounts.CreateAsync(new NewUser(
            tenantId, email, Password, phoneNumber!, role));

        return (userId, email);
    }

    private async Task SetEnabledAsync(Guid tenantId, Guid userId, bool enabled)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        await scope.ServiceProvider.GetRequiredService<IUserAccounts>()
            .SetAccountEnabledAsync(userId, enabled);
    }

    private HttpClient CreateClient(Guid tenantId, string role, Guid userId) =>
        factory.CreateAuthenticatedClient(tenantId, tenantId, role, userId);

    private async Task<string> ArrangeLiveSessionAsync(WebApplicationFactory<Program> app, string email)
    {
        var client = app.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = Password, deviceId = DeviceId });

        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var confirm = await client.PostAsJsonAsync(
            "/api/auth/confirm-device",
            new { email, deviceId = DeviceId, fingerprint = Fingerprint, otp = OtpCode });

        confirm.StatusCode.Should().Be(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());

        return confirm.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("stockma_refresh="));
    }

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { deviceId = DeviceId }),
        };
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private async Task SeedLiveSessionAsync(Guid tenantId, Guid userId)
    {
        var stampedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var uniqueHash = $"t069-{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, last_used_at, expires_at)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, {DeviceId}, 'fp-t069',
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

    private async Task<int> OtpCountAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        return await context.DeviceOtps.CountAsync(
            otp => otp.UserId == userId && otp.DeviceId == DeviceOtpService.PasswordResetScope);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;
}
