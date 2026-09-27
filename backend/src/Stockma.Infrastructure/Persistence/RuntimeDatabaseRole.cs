using Npgsql;

namespace Stockma.Infrastructure.Persistence;

public static class RuntimeDatabaseRole
{
    private const string InspectSql =
        """
        SELECT r.rolname,
               EXISTS (
                   SELECT 1
                     FROM pg_roles s
                    WHERE s.rolsuper
                      AND pg_has_role(r.oid, s.oid, 'MEMBER')),
               EXISTS (
                   SELECT 1
                     FROM pg_roles b
                    WHERE b.rolbypassrls
                      AND pg_has_role(r.oid, b.oid, 'MEMBER')),
               EXISTS (
                   SELECT 1
                     FROM pg_class c
                     JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'public'
                      AND c.relkind IN ('r', 'p')
                      AND pg_has_role(r.oid, c.relowner, 'MEMBER'))
          FROM pg_roles r
         WHERE r.rolname = current_user;
        """;

    public static async Task EnsureRestrictedAsync(string? connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Falta ConnectionStrings:Postgres: la API no sabe con qué rol conectarse.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(InspectSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        var role = reader.GetString(0);

        if (reader.GetBoolean(1))
        {
            throw Refuse(role, "es superusuario o miembro de un superusuario");
        }

        if (reader.GetBoolean(2))
        {
            throw Refuse(role, "tiene BYPASSRLS o es miembro de un rol con BYPASSRLS");
        }

        if (reader.GetBoolean(3))
        {
            throw Refuse(role, "es propietario de tablas del schema public (o miembro de su propietario, con o sin INHERIT)");
        }
    }

    private static InvalidOperationException Refuse(string role, string reason) =>
        new($"La API no arranca: el rol de runtime '{role}' {reason}, y la RLS no lo aislaría. "
            + "Conectate con app_user (ConnectionStrings:Postgres) y dejá el rol propietario sólo para migrar. Ver ADR-017.");
}
