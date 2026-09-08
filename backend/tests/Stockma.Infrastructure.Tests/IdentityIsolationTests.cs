using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Application.Identity;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class IdentityIsolationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string AppUserPassword = "app_user_test_pwd";
    private static readonly Guid TenantA = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TenantB = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid UserA = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UserB = Guid.Parse("88888888-8888-8888-8888-888888888888");

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

            INSERT INTO trusted_devices (id, tenant_id, user_id, device_id, fingerprint, trusted_at, expires_at)
            VALUES
                (gen_random_uuid(), '{TenantA}', '{UserA}', 'dev-a', 'fp-a', now(), now() + interval '15 days'),
                (gen_random_uuid(), '{TenantB}', '{UserB}', 'dev-b', 'fp-b', now(), now() + interval '15 days')
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
    public async Task Users_AreInvisibleAcrossTenants()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync("SELECT email FROM users ORDER BY email;", TenantA);

        rows.Should().ContainSingle().Which.Should().Be("ana@droga.co");
    }

    [Fact]
    public async Task Users_AreInvisibleWithoutActiveTenant()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync("SELECT email FROM users;", activeTenant: null);

        rows.Should().BeEmpty("fail-closed: sin app.tenant la política no devuelve ninguna fila");
    }

    [Fact]
    public async Task TrustedDevices_AreInvisibleAcrossTenants()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync("SELECT device_id FROM trusted_devices;", TenantB);

        rows.Should().ContainSingle().Which.Should().Be("dev-b");
    }

    [Fact]
    public async Task DeviceOtps_AreInvisibleWithoutActiveTenant()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync("SELECT device_id FROM device_otps;", activeTenant: null);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginLookup_FindsTheUserWithoutAnActiveTenant()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT tenant_id FROM auth_find_user_by_email('ANA@DROGA.CO');",
            activeTenant: null);

        rows.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                TenantA.ToString(),
                "ADR-002: el login corre sin contexto y resuelve el tenant desde el email");
    }

    [Fact]
    public async Task LoginLookup_FindsAUserOfAnyTenant()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT tenant_id FROM auth_find_user_by_email('BETO@OTRA.CO');",
            activeTenant: null);

        rows.Should().ContainSingle().Which.Should().Be(TenantB.ToString());
    }

    [Fact]
    public async Task LoginLookup_ReturnsNothingForAnUnknownEmail()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT tenant_id FROM auth_find_user_by_email('NADIE@NINGUNA.CO');",
            activeTenant: null);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginLookup_ExposesOnlyTheAuthenticationColumns()
    {
        await PrepareAsync();

        var exposed = await QueryAsAppUserAsync(
            """
            SELECT unnest(proargnames)
            FROM pg_proc
            WHERE proname = 'auth_find_user_by_email';
            """,
            activeTenant: null);

        exposed.Should()
            .BeEquivalentTo(
                [
                    "p_normalized_email",
                    "id",
                    "tenant_id",
                    "password_hash",
                    "security_stamp",
                    "lockout_end",
                    "lockout_enabled",
                ],
                "T055b: la excepción lee SÓLO lo necesario para autenticar y resolver el tenant. "
                + "Agregar una columna acá amplía el único agujero deliberado del aislamiento");
    }

    [Fact]
    public async Task LoginLookup_ReturnsAtMostOneRow()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync(
            "SELECT count(*) FROM auth_find_user_by_email('ANA@DROGA.CO');",
            activeTenant: null);

        rows.Should().ContainSingle().Which.Should().Be("1");
    }

    [Fact]
    public async Task TenantRoles_AreSeeded()
    {
        await PrepareAsync();

        var rows = await QueryAsAppUserAsync("SELECT name FROM roles ORDER BY name;", activeTenant: null);

        rows.Should()
            .BeEquivalentTo(
                [TenantRoles.Member, TenantRoles.TenantAdmin],
                "T062: no puede existir un tenant sin roles disponibles al crear su primer usuario");
    }
}
