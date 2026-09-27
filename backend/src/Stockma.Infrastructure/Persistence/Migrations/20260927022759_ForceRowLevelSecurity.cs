using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForceRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- ADR-017. Segunda llave: aunque el runtime se conecte por error con el rol
                -- propietario, FORCE hace que la politica tambien le aplique. Un superusuario
                -- o un rol con BYPASSRLS la siguen salteando: eso lo frena el chequeo de
                -- arranque de la API, no la base.
                ALTER TABLE tenant_settings FORCE ROW LEVEL SECURITY;
                ALTER TABLE products FORCE ROW LEVEL SECURITY;
                ALTER TABLE batches FORCE ROW LEVEL SECURITY;
                ALTER TABLE users FORCE ROW LEVEL SECURITY;
                ALTER TABLE trusted_devices FORCE ROW LEVEL SECURITY;
                ALTER TABLE device_otps FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.Sql("""
                -- Con FORCE, auth_find_user_by_email (SECURITY DEFINER) dejaria de ver filas
                -- si su duenio fuera el propietario de las tablas: corre sin app.tenant. Se
                -- descarto darle BYPASSRLS a un rol (saltea la RLS en TODA tabla) y
                -- `SET row_security = off` (exige superusuario). En su lugar, la funcion pasa
                -- a ser de auth_lookup: sin login, sin tablas propias, con SELECT sobre las
                -- columnas de autenticacion de `users` y una politica de lectura que aplica
                -- SOLO a ese rol. El resto de los roles sigue viendo unicamente su tenant.
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'auth_lookup') THEN
                        CREATE ROLE auth_lookup NOLOGIN;
                    END IF;
                END
                $$;

                -- Si auth_lookup ya existia (lo creo otra base del cluster u otro rol), el rol
                -- que migra necesita ADMIN sobre el para concederse la membresia de abajo. Un
                -- superusuario siempre lo tiene; PostgreSQL 16 se lo da solo a quien lo creo.
                DO $$
                BEGIN
                    IF NOT pg_has_role(current_user, 'auth_lookup', 'USAGE WITH ADMIN OPTION') THEN
                        RAISE EXCEPTION 'El rol que migra (%) no tiene ADMIN sobre auth_lookup, que ya existe en el cluster y lo creo otro rol. Un superusuario tiene que correr: GRANT auth_lookup TO % WITH ADMIN OPTION, INHERIT FALSE, SET FALSE; y despues se vuelve a migrar. Ver ADR-017.',
                            current_user, quote_ident(current_user);
                    END IF;
                END
                $$;

                GRANT USAGE ON SCHEMA public TO auth_lookup;
                GRANT SELECT (id, tenant_id, normalized_email, password_hash, security_stamp,
                              lockout_end, lockout_enabled)
                    ON users TO auth_lookup;

                CREATE POLICY auth_lookup_read ON users
                    FOR SELECT TO auth_lookup
                    USING (true);

                -- Cambiar el duenio exige poder asumir el rol destino y que ese rol tenga
                -- CREATE en el schema. Las dos concesiones son temporales, y la membresia va
                -- sin INHERIT: el rol que migra nunca hereda la politica de arriba.
                GRANT auth_lookup TO CURRENT_USER WITH INHERIT FALSE, SET TRUE;
                GRANT CREATE ON SCHEMA public TO auth_lookup;
                ALTER FUNCTION auth_find_user_by_email(text) OWNER TO auth_lookup;
                REVOKE CREATE ON SCHEMA public FROM auth_lookup;
                REVOKE auth_lookup FROM CURRENT_USER;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                GRANT auth_lookup TO CURRENT_USER WITH INHERIT TRUE, SET TRUE;
                ALTER FUNCTION auth_find_user_by_email(text) OWNER TO CURRENT_USER;
                REVOKE auth_lookup FROM CURRENT_USER;

                DROP POLICY IF EXISTS auth_lookup_read ON users;
                REVOKE ALL ON users FROM auth_lookup;
                REVOKE USAGE ON SCHEMA public FROM auth_lookup;

                ALTER TABLE device_otps NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE trusted_devices NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE users NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE batches NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE products NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE tenant_settings NO FORCE ROW LEVEL SECURITY;
                """);
        }
    }
}
