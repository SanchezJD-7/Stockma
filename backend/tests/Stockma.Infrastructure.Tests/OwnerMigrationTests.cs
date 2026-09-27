using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Stockma.Infrastructure.Persistence;
using Stockma.Infrastructure.Tenancy;

namespace Stockma.Infrastructure.Tests;

public class OwnerMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string OwnerPassword = "owner_pwd";

    private async Task<(string Owner, string AppUser)> CreateOwnedDatabaseAsync(bool adminOfLookupRole = true)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var owner = $"owner_{suffix}";
        var database = $"owned_{suffix}";

        await using (var connection = new NpgsqlConnection(postgres.ConnectionString))
        {
            await connection.OpenAsync();

            await using var createRole = new NpgsqlCommand(
                $"CREATE ROLE {owner} LOGIN CREATEROLE PASSWORD '{OwnerPassword}';", connection);
            await createRole.ExecuteNonQueryAsync();

            await using var createDatabase = new NpgsqlCommand(
                $"CREATE DATABASE {database} OWNER {owner};", connection);
            await createDatabase.ExecuteNonQueryAsync();

            if (adminOfLookupRole)
            {
                await using var grantAdmin = new NpgsqlCommand(
                    $"GRANT auth_lookup TO {owner} WITH ADMIN OPTION, INHERIT FALSE, SET FALSE;", connection);
                await grantAdmin.ExecuteNonQueryAsync();
            }
        }

        var ownerConnection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = database,
            Username = owner,
            Password = OwnerPassword,
        }.ConnectionString;

        var appUserConnection = new NpgsqlConnectionStringBuilder(postgres.AppUserConnectionString)
        {
            Database = database,
        }.ConnectionString;

        return (ownerConnection, appUserConnection);
    }

    private static StockmaDbContext OwnerContext(string connectionString)
    {
        var tenantContext = new TenantContext();
        tenantContext.Set(Guid.NewGuid());

        return new StockmaDbContext(
            new DbContextOptionsBuilder<StockmaDbContext>().UseNpgsql(connectionString).Options,
            tenantContext);
    }

    private static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    private static async Task SeedUserAsOwnerAsync(string ownerConnection, Guid tenantId, string normalizedEmail)
    {
        await using var connection = new NpgsqlConnection(ownerConnection);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO tenants (id) VALUES ('{tenantId}');
            SELECT set_config('app.tenant', '{tenantId}', false);
            INSERT INTO users (id, tenant_id, email, normalized_email, user_name, normalized_user_name,
                               email_confirmed, password_hash, security_stamp, concurrency_stamp,
                               phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES (gen_random_uuid(), '{tenantId}', lower('{normalizedEmail}'), '{normalizedEmail}',
                    lower('{normalizedEmail}'), '{normalizedEmail}', true, 'hash', 'stamp',
                    gen_random_uuid()::text, false, false, true, 0);
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Migrations_ApplyAsANonSuperuserOwner_AndTheLoginLookupStillFindsTheUser()
    {
        var (owner, appUser) = await CreateOwnedDatabaseAsync();
        await using (var context = OwnerContext(owner))
        {
            await context.Database.MigrateAsync();
        }

        var tenantId = Guid.NewGuid();
        await SeedUserAsOwnerAsync(owner, tenantId, "ANA@DROGA.CO");

        (await ScalarAsync(appUser, "SELECT tenant_id FROM auth_find_user_by_email('ANA@DROGA.CO');"))
            .Should()
            .Be(tenantId, "ADR-017: con FORCE el login sigue resolviendo el tenant desde el email");
    }

    [Fact]
    public async Task TheOwner_WithoutAnActiveTenant_SeesNoUsers()
    {
        var (owner, _) = await CreateOwnedDatabaseAsync();
        await using (var context = OwnerContext(owner))
        {
            await context.Database.MigrateAsync();
        }

        await SeedUserAsOwnerAsync(owner, Guid.NewGuid(), "BETO@OTRA.CO");

        (await ScalarAsync(owner, "SELECT count(*) FROM users;"))
            .Should()
            .Be(0L, "ADR-017: FORCE sujeta también al propietario, y migrar no lo hace miembro de auth_lookup");
    }

    [Fact]
    public async Task ForceRowLevelSecurity_RollsBackAndReappliesAsANonSuperuserOwner()
    {
        var (owner, _) = await CreateOwnedDatabaseAsync();
        await using var context = OwnerContext(owner);
        await context.Database.MigrateAsync();

        var migrator = context.GetService<IMigrator>();
        var act = async () =>
        {
            await migrator.MigrateAsync("HardenDeviceOtps");
            await migrator.MigrateAsync();
        };

        await act.Should().NotThrowAsync();
        (await ScalarAsync(owner, "SELECT relforcerowsecurity FROM pg_class WHERE relname = 'users';"))
            .Should()
            .Be(true);
    }

    [Fact]
    public async Task Migrations_AsAnOwnerWithoutAdminOnAnExistingAuthLookup_FailWithAnActionableMessage()
    {
        var (owner, _) = await CreateOwnedDatabaseAsync(adminOfLookupRole: false);
        await using var context = OwnerContext(owner);

        var act = () => context.Database.MigrateAsync();

        (await act.Should().ThrowAsync<PostgresException>(
                "ADR-017: auth_lookup ya existe en el cluster y lo creó otro rol"))
            .Which.MessageText.Should().Contain("auth_lookup").And.Contain("WITH ADMIN OPTION");
    }
}
