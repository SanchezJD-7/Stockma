using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Domain.Entities;
using Stockma.Domain.ValueObjects;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class PhoneNumberChangeEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string TenantHeader = "X-Tenant-ID";
    private const string CurrentPhone = "+573001234567";
    private const string NewPhone = "+573009876543";
    private const string AnotherPhone = "+573005555555";
    private const string OtpCode = "483920";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RequestChange_AsTheOwner_Returns202AndStoresThePendingTarget()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);

        var response = await CreateClient(tenantId, userId)
            .PutAsJsonAsync("/api/auth/phone-number", new { newPhoneNumber = NewPhone });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("otpSentToMasked").GetString().Should().Be(PhoneNumber.Mask(CurrentPhone));
        body.GetProperty("expiresInSeconds").GetInt32().Should().Be(600);

        var pending = await LivePhoneChangeOtpsAsync(tenantId, userId);
        pending.Should().ContainSingle()
            .Which.NewPhoneNumber.Should().Be(NewPhone, "el número nuevo vive en el OTP pendiente, no en ApplicationUser");
    }

    [Fact]
    public async Task RequestChange_ToTheSameNumber_Returns400()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);

        var response = await CreateClient(tenantId, userId)
            .PutAsJsonAsync("/api/auth/phone-number", new { newPhoneNumber = CurrentPhone });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task RequestChange_WithAnInvalidE164_Returns400()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);

        var response = await CreateClient(tenantId, userId)
            .PutAsJsonAsync("/api/auth/phone-number", new { newPhoneNumber = "3001234567" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task RequestChange_WithANumberFromAnotherCountry_Returns400()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);

        var response = await CreateClient(tenantId, userId)
            .PutAsJsonAsync("/api/auth/phone-number", new { newPhoneNumber = "+525512345678" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "el tenant es colombiano: un móvil mexicano no pasa");
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task RequestChange_WithoutAnEnrolledNumber_Returns403()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, phoneNumber: null);

        var response = await CreateClient(tenantId, userId)
            .PutAsJsonAsync("/api/auth/phone-number", new { newPhoneNumber = NewPhone });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "sin número cargado no hay canal vigente que probar");
        (await ErrorCodeAsync(response)).Should().Be("AUTH_PHONE_NOT_ENROLLED");
    }

    [Fact]
    public async Task RequestChange_WhenOneIsPending_Returns409AndKeepsTheOriginal()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);
        var client = CreateClient(tenantId, userId);

        var first = await client.PutAsJsonAsync("/api/auth/phone-number", new { newPhoneNumber = NewPhone });
        var second = await client.PutAsJsonAsync("/api/auth/phone-number", new { newPhoneNumber = AnotherPhone });

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict, "un solo cambio en vuelo: pedir otro devuelve 409");
        (await ErrorCodeAsync(second)).Should().Be("AUTH_PHONE_CHANGE_PENDING");

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        var all = await context.DeviceOtps
            .Where(otp => otp.UserId == userId)
            .OrderBy(otp => otp.Id)
            .ToListAsync();

        all.Should().ContainSingle("el segundo pedido no emite código nuevo");
        all[0].InvalidatedAt.Should().BeNull("el pendiente original sigue vivo esperando su confirmación");
        all[0].NewPhoneNumber.Should().Be(NewPhone);
    }

    [Fact]
    public async Task RequestChange_WithoutAToken_Returns401()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());

        var response = await client.PutAsJsonAsync(
            "/api/auth/phone-number",
            new { newPhoneNumber = NewPhone });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestChange_ForAnUnknownTokenUser_Returns404()
    {
        var tenantId = await SeedTenantAsync();
        var client = factory.CreateAuthenticatedClient(tenantId, tenantId, TenantRoles.Member);

        var response = await client.PutAsJsonAsync(
            "/api/auth/phone-number",
            new { newPhoneNumber = NewPhone });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Confirm_WithTheRightCode_AppliesTheNumber()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);
        await SeedPhoneChangeOtpAsync(tenantId, userId, OtpCode, NewPhone);

        var response = await CreateClient(tenantId, userId)
            .PostAsJsonAsync("/api/auth/phone-number/confirm", new { otp = OtpCode });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("phoneNumberMasked").GetString().Should().Be(PhoneNumber.Mask(NewPhone));
        body.GetProperty("phoneNumberConfirmed").GetBoolean().Should().BeTrue();

        var stored = await GetUserAsync(tenantId, userId);
        stored!.PhoneNumber.Should().Be(NewPhone);
        stored.PhoneNumberConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task Confirm_WithTheWrongCode_Returns401AndKeepsTheNumber()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);
        await SeedPhoneChangeOtpAsync(tenantId, userId, OtpCode, NewPhone);

        var response = await CreateClient(tenantId, userId)
            .PostAsJsonAsync("/api/auth/phone-number/confirm", new { otp = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(response)).Should().Be("AUTH_OTP_REJECTED");

        var stored = await GetUserAsync(tenantId, userId);
        stored!.PhoneNumber.Should().Be(CurrentPhone, "sin OTP válido el número queda intacto");
    }

    [Fact]
    public async Task Confirm_WithoutAPendingChange_Returns401()
    {
        var tenantId = await SeedTenantAsync();
        var userId = await SeedUserAsync(tenantId, CurrentPhone);

        var response = await CreateClient(tenantId, userId)
            .PostAsJsonAsync("/api/auth/phone-number/confirm", new { otp = OtpCode });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no hay OTP vigente que consumir");
        (await ErrorCodeAsync(response)).Should().Be("AUTH_OTP_REJECTED");
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

    private async Task<Guid> SeedUserAsync(Guid tenantId, string? phoneNumber)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();

        return await accounts.CreateAsync(new NewUser(
            tenantId,
            $"{Guid.NewGuid():N}@droga.co",
            "Contrasena-Larga-1",
            phoneNumber ?? string.Empty,
            TenantRoles.Member));
    }

    private HttpClient CreateClient(Guid tenantId, Guid userId) =>
        factory.CreateAuthenticatedClient(tenantId, tenantId, TenantRoles.Member, userId);

    private async Task SeedPhoneChangeOtpAsync(Guid tenantId, Guid userId, string code, string newPhoneNumber)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        user.Should().NotBeNull();

        var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>()
            .HashPassword(user!, code);

        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO device_otps (id, tenant_id, user_id, device_id, code_hash,
                                     issued_at, expires_at, failed_attempts, new_phone_number)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, 'internal:phone-change', {hash},
                    {issuedAt}, {issuedAt.AddMinutes(10)}, 0, {newPhoneNumber});
            """);
    }

    private async Task<List<DeviceOtp>> LivePhoneChangeOtpsAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        return await context.DeviceOtps
            .Where(otp => otp.UserId == userId
                && otp.DeviceId == "internal:phone-change"
                && otp.ConsumedAt == null
                && otp.InvalidatedAt == null)
            .ToListAsync();
    }

    private async Task<ApplicationUser?> GetUserAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        return await userManager.FindByIdAsync(userId.ToString());
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;
}
