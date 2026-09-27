using FluentAssertions;
using Npgsql;
using Stockma.Infrastructure.Persistence;

namespace Stockma.Infrastructure.Tests;

public class RuntimeDatabaseRoleTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string ProbePassword = "probe_pwd";

    private async Task<string> CreateLoginRoleAsync(string attributes, bool ownsATable = false)
    {
        var role = $"probe_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        var sql = $"CREATE ROLE {role} LOGIN PASSWORD '{ProbePassword}' {attributes};";

        if (ownsATable)
        {
            sql += $" CREATE TABLE {role}_owned (id int); ALTER TABLE {role}_owned OWNER TO {role};";
        }

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Username = role,
            Password = ProbePassword,
        }.ConnectionString;
    }

    [Fact]
    public async Task EnsureRestricted_AsTheNonOwnerAppUser_Passes()
    {
        var act = () => RuntimeDatabaseRole.EnsureRestrictedAsync(postgres.AppUserConnectionString);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureRestricted_AsASuperuser_Throws()
    {
        var act = () => RuntimeDatabaseRole.EnsureRestrictedAsync(postgres.ConnectionString);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("superusuario");
    }

    [Fact]
    public async Task EnsureRestricted_AsARoleWithBypassRls_Throws()
    {
        var connectionString = await CreateLoginRoleAsync("BYPASSRLS");

        var act = () => RuntimeDatabaseRole.EnsureRestrictedAsync(connectionString);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("BYPASSRLS");
    }

    [Fact]
    public async Task EnsureRestricted_AsATableOwner_Throws()
    {
        var connectionString = await CreateLoginRoleAsync(string.Empty, ownsATable: true);

        var act = () => RuntimeDatabaseRole.EnsureRestrictedAsync(connectionString);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("propietario");
    }

    [Fact]
    public async Task EnsureRestricted_AsAMemberOfATableOwner_Throws()
    {
        var ownerConnection = await CreateLoginRoleAsync(string.Empty, ownsATable: true);
        var owner = new NpgsqlConnectionStringBuilder(ownerConnection).Username!;
        var memberConnection = await CreateLoginRoleAsync($"IN ROLE {owner}");

        var act = () => RuntimeDatabaseRole.EnsureRestrictedAsync(memberConnection);

        (await act.Should().ThrowAsync<InvalidOperationException>(
                "PostgreSQL trata como propietario a quien hereda los privilegios del rol dueño"))
            .Which.Message.Should().Contain("propietario");
    }

    [Fact]
    public async Task EnsureRestricted_AsANonInheritingMemberOfATableOwner_Throws()
    {
        var ownerConnection = await CreateLoginRoleAsync(string.Empty, ownsATable: true);
        var owner = new NpgsqlConnectionStringBuilder(ownerConnection).Username!;
        var memberConnection = await CreateLoginRoleAsync($"NOINHERIT IN ROLE {owner}");

        var act = () => RuntimeDatabaseRole.EnsureRestrictedAsync(memberConnection);

        (await act.Should().ThrowAsync<InvalidOperationException>(
                "sin INHERIT igual puede hacer SET ROLE al dueño y saltear la RLS"))
            .Which.Message.Should().Contain("propietario");
    }

    [Fact]
    public async Task EnsureRestricted_AsAMemberOfABypassRlsRole_Throws()
    {
        var bypassConnection = await CreateLoginRoleAsync("BYPASSRLS");
        var bypass = new NpgsqlConnectionStringBuilder(bypassConnection).Username!;
        var memberConnection = await CreateLoginRoleAsync($"IN ROLE {bypass}");

        var act = () => RuntimeDatabaseRole.EnsureRestrictedAsync(memberConnection);

        (await act.Should().ThrowAsync<InvalidOperationException>(
                "puede hacer SET ROLE a un rol con BYPASSRLS"))
            .Which.Message.Should().Contain("BYPASSRLS");
    }

    [Fact]
    public async Task EnsureRestricted_AsAMemberOfASuperuser_Throws()
    {
        var superuserConnection = await CreateLoginRoleAsync("SUPERUSER");
        var superuser = new NpgsqlConnectionStringBuilder(superuserConnection).Username!;
        var memberConnection = await CreateLoginRoleAsync($"NOINHERIT IN ROLE {superuser}");

        var act = () => RuntimeDatabaseRole.EnsureRestrictedAsync(memberConnection);

        (await act.Should().ThrowAsync<InvalidOperationException>(
                "puede hacer SET ROLE a un superusuario"))
            .Which.Message.Should().Contain("superusuario");
    }
}
