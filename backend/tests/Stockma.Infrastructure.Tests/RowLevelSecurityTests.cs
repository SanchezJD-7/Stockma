using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stockma.Infrastructure.Persistence;
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
}
