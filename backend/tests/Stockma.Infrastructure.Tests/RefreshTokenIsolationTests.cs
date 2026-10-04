using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class RefreshTokenIsolationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string AppUserPassword = "app_user_test_pwd";
    private static readonly Guid TenantA = Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1");
    private static readonly Guid TenantB = Guid.Parse("d2d2d2d2-d2d2-d2d2-d2d2-d2d2d2d2d2d2");
    private static readonly Guid UserA = Guid.Parse("e3e3e3e3-e3e3-e3e3-e3e3-e3e3e3e3e3e3");
    private static readonly Guid UserB = Guid.Parse("f4f4f4f4-f4f4-f4f4-f4f4-f4f4f4f4f4f4");
    private static readonly Guid FamilyA = Guid.Parse("a5a5a5a5-a5a5-a5a5-a5a5-a5a5a5a5a5a5");
    private static readonly Guid FamilyB = Guid.Parse("b6b6b6b6-b6b6-b6b6-b6b6-b6b6b6b6b6b6");
    private static readonly Guid TokenA = Guid.Parse("17171717-1717-1717-1717-171717171717");
    private static readonly Guid TokenB = Guid.Parse("28282828-2828-2828-2828-282828282828");

    private string AppUserConnectionString =>
        new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Username = "app_user",
            Password = AppUserPassword,
        }.ConnectionString;

    private async Task PrepareAsync()
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(TenantA);

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        await using var context = new StockmaDbContext(options, tenantContext);
        await context.Database.MigrateAsync();

#pragma warning disable EF1002
        await context.Database.ExecuteSqlRawAsync(
            $"""
            ALTER ROLE app_user PASSWORD '{AppUserPassword}';

            INSERT INTO tenants (id) VALUES ('{TenantA}'), ('{TenantB}') ON CONFLICT DO NOTHING;

            INSERT INTO users (id, tenant_id, email, normalized_email, user_name, normalized_user_name,
                               email_confirmed, password_hash, security_stamp, concurrency_stamp,
                               phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES
                ('{UserA}', '{TenantA}', 'ana@droga.co', 'ANA@DROGA.CO', 'ana@droga.co', 'ANA@DROGA.CO',
                 true, 'hash-a', 'stamp-a', gen_random_uuid()::text, false, false, true, 0),
                ('{UserB}', '{TenantB}', 'beto@otra.co', 'BETO@OTRA.CO', 'beto@otra.co', 'BETO@OTRA.CO',
                 true, 'hash-b', 'stamp-b', gen_random_uuid()::text, false, false, true, 0)
            ON CONFLICT DO NOTHING;

            INSERT INTO refresh_tokens (id, tenant_id, user_id, family_id, device_id, token_hash,
                                        issued_at, expires_at, family_expires_at)
            VALUES
                ('{TokenA}', '{TenantA}', '{UserA}', '{FamilyA}', 'dev-a', 'hash-token-a',
                 now(), now() + interval '30 minutes', now() + interval '8 hours'),
                ('{TokenB}', '{TenantB}', '{UserB}', '{FamilyB}', 'dev-b', 'hash-token-b',
                 now(), now() + interval '30 minutes', now() + interval '8 hours')
            ON CONFLICT DO NOTHING;
            """);
#pragma warning restore EF1002
    }

    private async Task<List<string>> QueryAsAppUserAsync(string sql, Guid? activeTenant)
    {
        await using var connection = new NpgsqlConnection(AppUserConnectionString);
        await connection.OpenAsync();

        if (activeTenant is not null)
        {
            await using var setTenant = connection.CreateCommand();
            setTenant.CommandText = "SELECT set_config('app.tenant', @tenant, false);";
            setTenant.Parameters.AddWithValue("tenant", activeTenant.Value.ToString());
            await setTenant.ExecuteNonQueryAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetValue(0).ToString()!);
        }

        return rows;
    }

    [Fact]
    public async Task RefreshTokens_AreInvisibleAcrossTenants()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync("SELECT token_hash FROM refresh_tokens;", TenantB);

        rows.Should().ContainSingle().Which.Should().Be("hash-token-b");
    }

    [Fact]
    public async Task RefreshTokens_AreInvisibleWithoutActiveTenant()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync("SELECT token_hash FROM refresh_tokens;", activeTenant: null);

        rows.Should().BeEmpty("fail-closed: sin app.tenant la política no devuelve ninguna fila (ADR-017)");
    }

    [Fact]
    public async Task Lookup_FindsTheTokenWithoutAnActiveTenant()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT tenant_id FROM auth_find_refresh_token_by_hash('hash-token-a');",
            activeTenant: null);

        rows.Should()
            .ContainSingle()
            .Which.Should()
            .Be(TenantA.ToString(), "T065/2b: refresh y logout resuelven el tenant desde la fila del token, sin X-Tenant-ID");
    }

    [Fact]
    public async Task Lookup_FindsAnyTenantsToken()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT tenant_id FROM auth_find_refresh_token_by_hash('hash-token-b');",
            activeTenant: null);

        rows.Should().ContainSingle().Which.Should().Be(TenantB.ToString());
    }

    [Fact]
    public async Task Lookup_ForAnUnknownHash_ReturnsNothing()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT tenant_id FROM auth_find_refresh_token_by_hash('no-such-hash');",
            activeTenant: null);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Lookup_NeverProjectsTheTokenHashItself()
    {
        await PrepareAsync();

        var act = () => QueryAsAppUserAsync(
            "SELECT token_hash FROM auth_find_refresh_token_by_hash('hash-token-a');",
            activeTenant: null);

        (await act.Should().ThrowAsync<PostgresException>(
                "la función devuelve id, tenant_id, user_id, family_id, device_id y las fechas — nunca el hash (ADR-018)"))
            .Which.SqlState.Should().Be("42703");
    }

    [Fact]
    public async Task Lookup_ForATokenOfAnotherHash_ReturnsOnlyTheMatchingOne()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT id FROM auth_find_refresh_token_by_hash('hash-token-a');",
            activeTenant: null);

        rows.Should()
            .ContainSingle()
            .Which.Should()
            .Be(TokenA.ToString(), "sólo la fila del hash exacto vuelve, nunca la de otro hash");
    }

    [Fact]
    public async Task Lookup_ReturnsAtMostOneRow()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT count(*) FROM auth_find_refresh_token_by_hash('hash-token-a');",
            activeTenant: null);

        rows.Should().ContainSingle().Which.Should().Be("1");
    }

    [Fact]
    public async Task Lookup_ExposesOnlyTheColumnsThatTwoCNeedsToResolveTheSession()
    {
        await PrepareAsync();

        var exposed = await QueryAsAppUserAsync(
            """
            SELECT unnest(proargnames)
            FROM pg_proc
            WHERE proname = 'auth_find_refresh_token_by_hash';
            """,
            activeTenant: null);

        exposed.Should()
            .BeEquivalentTo(
                [
                    "p_token_hash",
                    "id",
                    "tenant_id",
                    "user_id",
                    "family_id",
                    "device_id",
                    "expires_at",
                    "family_expires_at",
                    "consumed_at",
                    "revoked_at",
                ],
                "el hash NUNCA se devuelve, sólo se usa para filtrar (ADR-018)");
    }

    [Fact]
    public async Task LookupRole_RunsAsADedicatedRoleThatOwnsNoTableAndCannotBypassRls()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            """
            SELECT concat_ws('|', r.rolname, r.rolsuper::text, r.rolbypassrls::text, r.rolcanlogin::text,
                   (EXISTS (SELECT 1 FROM pg_class c WHERE c.relowner = r.oid))::text)
              FROM pg_proc p
              JOIN pg_roles r ON r.oid = p.proowner
             WHERE p.proname = 'auth_find_refresh_token_by_hash';
            """,
            activeTenant: null);

        rows.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                "auth_lookup|false|false|false|false",
                "ADR-017: la misma receta que auth_find_user_by_email — sin login, sin BYPASSRLS, sin tablas propias");
    }

    [Fact]
    public async Task DuplicateTokenHash_IsRejectedAtTheDatabase()
    {
        await PrepareAsync();

        await using var connection = new NpgsqlConnection(AppUserConnectionString);
        await connection.OpenAsync();

        await using var setTenant = connection.CreateCommand();
        setTenant.CommandText = "SELECT set_config('app.tenant', @tenant, false);";
        setTenant.Parameters.AddWithValue("tenant", TenantA.ToString());
        await setTenant.ExecuteNonQueryAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            INSERT INTO refresh_tokens (id, tenant_id, user_id, family_id, device_id, token_hash,
                                        issued_at, expires_at, family_expires_at)
            VALUES (gen_random_uuid(), '{TenantA}', '{UserA}', gen_random_uuid(), 'dev-a', 'hash-token-a',
                    now(), now() + interval '30 minutes', now() + interval '8 hours');
            """;

        var act = () => command.ExecuteNonQueryAsync();

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }
}
