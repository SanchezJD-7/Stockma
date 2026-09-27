using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Persistence.Interceptors;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class RowLevelSecurityTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string AppUserPassword = "app_user_test_pwd";
    private static readonly Guid TenantA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TenantB = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private string AppUserConnectionString =>
        new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Username = "app_user",
            Password = AppUserPassword,
        }.ConnectionString;

    private async Task PrepareSchemaAndDataAsync()
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(TenantA);

        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options;

        await using var context = new StockmaDbContext(options, tenantContext);
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync(
            $"""
            ALTER ROLE app_user PASSWORD '{AppUserPassword}';

            INSERT INTO tenants (id) VALUES ('{TenantA}'), ('{TenantB}') ON CONFLICT DO NOTHING;

            INSERT INTO tenant_settings (tenant_id, max_trusted_devices, green_months, yellow_months)
            VALUES ('{TenantA}', 2, 6, 3), ('{TenantB}', 5, 9, 4) ON CONFLICT DO NOTHING;
            """);
    }

    private async Task<List<Guid>> QueryAsAppUserAsync(Guid? activeTenant)
    {
        await using var connection = new NpgsqlConnection(AppUserConnectionString);
        await connection.OpenAsync();

        if (activeTenant.HasValue)
        {
            await using var setTenant = new NpgsqlCommand(
                $"SET app.tenant = '{activeTenant.Value}';", connection);
            await setTenant.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(
            "SELECT tenant_id FROM tenant_settings;", connection);

        var rows = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetGuid(0));
        }

        return rows;
    }

    [Fact]
    public async Task WithoutActiveTenant_ReturnsNoRows()
    {
        await PrepareSchemaAndDataAsync();

        var rows = await QueryAsAppUserAsync(activeTenant: null);

        rows.Should().BeEmpty(
            "sin app.tenant definido la política evalúa NULL y no debe filtrar hacia afuera (fail-closed)");
    }

    [Fact]
    public async Task WithTenantA_ReturnsOnlyTenantARows()
    {
        await PrepareSchemaAndDataAsync();

        var rows = await QueryAsAppUserAsync(TenantA);

        rows.Should().ContainSingle().Which.Should().Be(TenantA);
    }

    [Fact]
    public async Task WithTenantB_DoesNotReturnTenantARows()
    {
        await PrepareSchemaAndDataAsync();

        var rows = await QueryAsAppUserAsync(TenantB);

        rows.Should().ContainSingle().Which.Should().Be(TenantB);
        rows.Should().NotContain(TenantA, "un tenant nunca debe ver datos de otro (NFR-001)");
    }

    [Fact]
    public async Task AppUser_MustNotOwnTheTables()
    {
        await PrepareSchemaAndDataAsync();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT tableowner FROM pg_tables WHERE tablename = 'tenant_settings';", connection);

        var owner = (string?)await command.ExecuteScalarAsync();

        owner.Should().NotBe(
            "app_user",
            "PostgreSQL no aplica políticas RLS al propietario de la tabla: si la aplicación se conectara con el rol propietario, la capa 2 del aislamiento no filtraría nada");
    }

    [Fact]
    public async Task RowLevelSecurity_MustBeEnabledOnTenantTables()
    {
        await PrepareSchemaAndDataAsync();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT relrowsecurity FROM pg_class WHERE relname = 'tenant_settings';", connection);

        var enabled = (bool?)await command.ExecuteScalarAsync();

        enabled.Should().BeTrue("la migración InitialSchema debe habilitar RLS en toda tabla tenant (FR-003)");
    }

    [Fact]
    public async Task RowLevelSecurity_IsForcedOnEveryTableThatEnablesIt()
    {
        await PrepareSchemaAndDataAsync();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT c.relname, c.relforcerowsecurity
              FROM pg_class c
              JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = 'public' AND c.relrowsecurity
             ORDER BY c.relname;
            """,
            connection);

        var tables = new Dictionary<string, bool>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables[reader.GetString(0)] = reader.GetBoolean(1);
        }

        tables.Keys.Should().BeEquivalentTo(
            ["batches", "device_otps", "products", "tenant_settings", "trusted_devices", "users"]);
        tables.Should().AllSatisfy(table => table.Value.Should().BeTrue(
            $"ADR-017: sin FORCE, el propietario de '{table.Key}' queda fuera de la política"));
    }

    [Fact]
    public async Task TheTableOwner_WithoutAnActiveTenant_SeesNoRows()
    {
        await PrepareSchemaAndDataAsync();

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var command = new NpgsqlCommand(
            """
            CREATE ROLE rls_owner_probe NOLOGIN;
            ALTER TABLE tenant_settings OWNER TO rls_owner_probe;
            SET LOCAL ROLE rls_owner_probe;
            SELECT count(*) FROM tenant_settings;
            """,
            connection,
            transaction);

        var visible = (long?)await command.ExecuteScalarAsync();

        await transaction.RollbackAsync();

        visible.Should().Be(
            0,
            "ADR-017: con FORCE ROW LEVEL SECURITY el propietario también queda sujeto a la política fail-closed");
    }

    private static async Task<(int BackendPid, long VisibleRows)> ReadThroughContextAsync(
        string connectionString,
        TenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<StockmaDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;

        await using var context = new StockmaDbContext(options, tenantContext);
        await context.Database.OpenConnectionAsync();

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_backend_pid(), (SELECT count(*) FROM tenant_settings);";

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        return (reader.GetInt32(0), reader.GetInt64(1));
    }

    [Fact]
    public async Task APooledConnection_ReusedWithoutATenant_SeesNoRows()
    {
        await PrepareSchemaAndDataAsync();

        var singleConnectionPool = new NpgsqlConnectionStringBuilder(AppUserConnectionString)
        {
            MaxPoolSize = 1,
            ApplicationName = $"pool-probe-{Guid.NewGuid():N}",
        }.ConnectionString;

        var tenantA = new TenantContext();
        tenantA.Set(TenantA);

        var first = await ReadThroughContextAsync(singleConnectionPool, tenantA);
        var reused = await ReadThroughContextAsync(singleConnectionPool, new TenantContext());

        reused.BackendPid.Should().Be(first.BackendPid, "la segunda lectura tiene que reusar la conexión física del pool");
        first.VisibleRows.Should().Be(1);
        reused.VisibleRows.Should().Be(
            0,
            "ADR-017: app.tenant no sobrevive a la devolución al pool; sin tenant, la conexión reusada es fail-closed");
    }
}
