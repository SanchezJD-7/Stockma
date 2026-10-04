using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stockma.Domain.Entities;
using Stockma.Infrastructure.Identity;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class RefreshTokensTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    private const string DeviceId = "device-mostrador-0001";
    private const int SessionIdleTimeoutMinutes = 30;
    private const int RefreshTokenLifetimeHours = 8;

    private StockmaDbContext NewContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        return postgres.CreateAppUserContext(tenantContext);
    }

    private StockmaDbContext NewContextWithoutTenant() => postgres.CreateAppUserContext(new TenantContext());

    private async Task<(RefreshTokens Tokens, StockmaDbContext Context, Guid TenantId, Guid UserId)> BuildAsync()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId);

        await using (var seeding = new StockmaDbContext(
            new DbContextOptionsBuilder<StockmaDbContext>().UseNpgsql(postgres.ConnectionString).Options,
            tenantContext))
        {
            await seeding.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO tenants (id) VALUES ({0}) ON CONFLICT DO NOTHING;

                INSERT INTO users (id, tenant_id, email, normalized_email, user_name, normalized_user_name,
                                   email_confirmed, password_hash, security_stamp, concurrency_stamp,
                                   phone_number, phone_number_confirmed, two_factor_enabled,
                                   lockout_enabled, access_failed_count)
                VALUES ({1}, {0}, {2}, {3}, {2}, {3}, true, 'hash', 'stamp', gen_random_uuid()::text,
                        '+573001234567', false, false, true, 0);
                """,
                tenantId,
                userId,
                $"{userId:N}@droga.co",
                $"{userId:N}@DROGA.CO".ToUpperInvariant());
        }

        var context = NewContext(tenantId);

        return (new RefreshTokens(context), context, tenantId, userId);
    }

    private static RefreshToken NewFamilyToken(Guid tenantId, Guid userId, string tokenHash) =>
        RefreshToken.IssueForNewFamily(
            tenantId,
            userId,
            DeviceId,
            tokenHash,
            Now,
            SessionIdleTimeoutMinutes,
            RefreshTokenLifetimeHours);

    [Fact]
    public async Task TheAdapterUnderTest_RunsAsTheRestrictedAppUser()
    {
        var (_, context, _, _) = await BuildAsync();

        var role = await context.Database.SqlQueryRaw<string>("SELECT current_user::text AS \"Value\"").SingleAsync();

        role.Should().Be("app_user", "ADR-017: el alta y el consumo de refresh tokens corren bajo RLS, no como superusuario");
    }

    [Fact]
    public async Task AddAsync_PersistsTheToken()
    {
        var (tokens, context, tenantId, userId) = await BuildAsync();
        var token = NewFamilyToken(tenantId, userId, "hash-add-1");

        await tokens.AddAsync(token);

        var stored = await context.RefreshTokens.SingleAsync(t => t.Id == token.Id);
        stored.TokenHash.Should().Be("hash-add-1");
    }

    [Fact]
    public async Task AddAsync_NeverPersistsAnythingThatLooksLikeThePlainToken()
    {
        var (tokens, context, tenantId, userId) = await BuildAsync();
        var plainToken = "PLAINTEXT-TOKEN-SHOULD-NEVER-BE-STORED";
        var tokenHash = Stockma.Domain.ValueObjects.RefreshTokenMaterial.Hash(plainToken);
        var token = NewFamilyToken(tenantId, userId, tokenHash);

        await tokens.AddAsync(token);

        var stored = await context.RefreshTokens.SingleAsync(t => t.Id == token.Id);
        stored.TokenHash.Should().NotBe(plainToken).And.Be(tokenHash);
        stored.TokenHash.Should().NotContain("PLAINTEXT");
    }

    [Fact]
    public async Task AddAsync_ADuplicateTokenHash_IsRejected()
    {
        var (tokens, _, tenantId, userId) = await BuildAsync();
        await tokens.AddAsync(NewFamilyToken(tenantId, userId, "duplicate-hash"));

        var otherUserId = Guid.CreateVersion7();
        await using var seeding = new StockmaDbContext(
            new DbContextOptionsBuilder<StockmaDbContext>().UseNpgsql(postgres.ConnectionString).Options,
            new TenantContext());
        await seeding.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO users (id, tenant_id, email, normalized_email, user_name, normalized_user_name,
                               email_confirmed, password_hash, security_stamp, concurrency_stamp,
                               phone_number, phone_number_confirmed, two_factor_enabled,
                               lockout_enabled, access_failed_count)
            VALUES ({0}, {1}, {2}, {3}, {2}, {3}, true, 'hash', 'stamp', gen_random_uuid()::text,
                    '+573001234567', false, false, true, 0);
            """,
            otherUserId,
            tenantId,
            $"{otherUserId:N}@droga.co",
            $"{otherUserId:N}@DROGA.CO".ToUpperInvariant());

        var act = () => tokens.AddAsync(NewFamilyToken(tenantId, otherUserId, "duplicate-hash"));

        await act.Should().ThrowAsync<DbUpdateException>("ux_refresh_tokens_token_hash es unico (ADR-018)");
    }

    [Fact]
    public async Task FindByHashAsync_ThroughTheAdapter_FindsTheTokenWithoutATenant()
    {
        var (tokens, _, tenantId, userId) = await BuildAsync();
        var token = NewFamilyToken(tenantId, userId, "hash-lookup-1");
        await tokens.AddAsync(token);

        var noTenantTokens = new RefreshTokens(NewContextWithoutTenant());
        var lookup = await noTenantTokens.FindByHashAsync("hash-lookup-1");

        lookup.Should().NotBeNull();
        lookup!.TenantId.Should().Be(tenantId);
        lookup.UserId.Should().Be(userId);
        lookup.FamilyId.Should().Be(token.FamilyId);
    }

    [Fact]
    public async Task FindByHashAsync_ForAnUnknownHash_ReturnsNull()
    {
        var (_, _, tenantId, _) = await BuildAsync();
        var tokens = new RefreshTokens(NewContext(tenantId));

        (await tokens.FindByHashAsync("no-existe-este-hash")).Should().BeNull();
    }
}
