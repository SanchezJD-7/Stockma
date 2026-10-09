using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Api.Tests;

public class AdminUserRoleEndpointsTests(StockmaApiFactory factory) : IClassFixture<StockmaApiFactory>
{
    private const string TenantHeader = "X-Tenant-ID";
    private const string DeviceId = "web-chrome-a91f2c77";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SetUserRole_AsAnAdmin_PromotesAMemberWithoutKillingTheSession()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var memberId = await SeedUserAsync(tenantId, TenantRoles.Member);
        await SeedLiveSessionAsync(tenantId, memberId);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PutAsJsonAsync($"/api/admin/users/{memberId}/role", new { role = TenantRoles.TenantAdmin });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("userId").GetGuid().Should().Be(memberId);
        body.GetProperty("role").GetString().Should().Be(TenantRoles.TenantAdmin);

        (await GetRolesAsync(tenantId, memberId)).Should().Contain(TenantRoles.TenantAdmin);
        (await AnyLiveSessionAsync(tenantId, memberId)).Should()
            .BeTrue("dar poder no expulsa: el rol nuevo llega con el próximo refresh");
    }

    [Fact]
    public async Task SetUserRole_DemotingAnAdmin_CutsEverySessionOfThatUser()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        await SeedLiveSessionAsync(tenantId, targetId);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PutAsJsonAsync($"/api/admin/users/{targetId}/role", new { role = TenantRoles.Member });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await GetRolesAsync(tenantId, targetId)).Should()
            .Contain(TenantRoles.Member)
            .And.NotContain(TenantRoles.TenantAdmin);
        (await AnyLiveSessionAsync(tenantId, targetId)).Should()
            .BeFalse("quitar poder obliga a re-login con claims frescos");
    }

    [Fact]
    public async Task SetUserRole_DemotingTheOnlyAdmin_Returns400AndKeepsTheRole()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PutAsJsonAsync($"/api/admin/users/{adminId}/role", new { role = TenantRoles.Member });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
        (await GetRolesAsync(tenantId, adminId)).Should().Contain(TenantRoles.TenantAdmin);
    }

    [Fact]
    public async Task SetUserRole_WithAnUnknownRole_Returns400()
    {
        var tenantId = await SeedTenantAsync();
        var adminId = await SeedUserAsync(tenantId, TenantRoles.TenantAdmin);
        var memberId = await SeedUserAsync(tenantId, TenantRoles.Member);

        var response = await CreateClient(tenantId, TenantRoles.TenantAdmin, adminId)
            .PutAsJsonAsync($"/api/admin/users/{memberId}/role", new { role = "SuperAdmin" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("VALIDATION_FAILED");
        (await GetRolesAsync(tenantId, memberId)).Should().NotContain("SuperAdmin");
    }

    [Fact]
    public async Task SetUserRole_AsAMember_Returns403()
    {
        var tenantId = await SeedTenantAsync();
        var callerId = await SeedUserAsync(tenantId, TenantRoles.Member);
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);

        var response = await CreateClient(tenantId, TenantRoles.Member, callerId)
            .PutAsJsonAsync($"/api/admin/users/{targetId}/role", new { role = TenantRoles.TenantAdmin });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "T094: sólo un admin del tenant toca roles");
    }

    [Fact]
    public async Task SetUserRole_ForAUserOfAnotherTenant_Returns404()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();
        var adminA = await SeedUserAsync(tenantA, TenantRoles.TenantAdmin);
        var userB = await SeedUserAsync(tenantB, TenantRoles.Member);

        var response = await CreateClient(tenantA, TenantRoles.TenantAdmin, adminA)
            .PutAsJsonAsync($"/api/admin/users/{userB}/role", new { role = TenantRoles.TenantAdmin });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "T094: roles aislados por tenant (FR-002)");
        (await ErrorCodeAsync(response)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task SetUserRole_WithoutAToken_Returns401()
    {
        var tenantId = await SeedTenantAsync();
        var targetId = await SeedUserAsync(tenantId, TenantRoles.Member);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantHeader, tenantId.ToString());

        var response = await client.PutAsJsonAsync(
            $"/api/admin/users/{targetId}/role",
            new { role = TenantRoles.TenantAdmin });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        var uniqueHash = $"t094-{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<StockmaDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint,
                                         trusted_at, last_used_at, expires_at)
            VALUES (gen_random_uuid(), {tenantId}, {userId}, {DeviceId}, 'fp-t094',
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

    private async Task<IList<string>> GetRolesAsync(Guid tenantId, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());

        user.Should().NotBeNull();

        return await userManager.GetRolesAsync(user!);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;
}
