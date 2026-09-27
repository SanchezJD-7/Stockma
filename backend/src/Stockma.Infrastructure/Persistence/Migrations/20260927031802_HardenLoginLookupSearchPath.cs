using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenLoginLookupSearchPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SetSearchPath("pg_catalog, public, pg_temp"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SetSearchPath("public"));
        }

        private static string SetSearchPath(string searchPath) =>
            $"""
            -- ADR-017. auth_find_user_by_email es SECURITY DEFINER: con `SET search_path = public`
            -- PostgreSQL igual busca primero en pg_temp, y una tabla temporal `users` creada por
            -- app_user reemplazaria a la real dentro de la funcion. Poner pg_temp explicito y al
            -- final cierra eso; pg_catalog va primero para que tampoco se pisen sus funciones.
            --
            -- La funcion es de auth_lookup: solo su duenio la puede alterar. El rol que migra se
            -- concede la membresia sin INHERIT, la asume con SET ROLE y la devuelve en el acto,
            -- igual que ForceRowLevelSecurity; por eso necesita ADMIN sobre auth_lookup.
            DO $$
            BEGIN
                IF NOT pg_has_role(current_user, 'auth_lookup', 'USAGE WITH ADMIN OPTION') THEN
                    RAISE EXCEPTION 'El rol que migra (%) no tiene ADMIN sobre auth_lookup, que ya existe en el cluster y lo creo otro rol. Un superusuario tiene que correr: GRANT auth_lookup TO % WITH ADMIN OPTION, INHERIT FALSE, SET FALSE; y despues se vuelve a migrar. Ver ADR-017.',
                        current_user, quote_ident(current_user);
                END IF;
            END
            $$;

            GRANT auth_lookup TO CURRENT_USER WITH INHERIT FALSE, SET TRUE;
            SET ROLE auth_lookup;
            ALTER FUNCTION public.auth_find_user_by_email(text) SET search_path = {searchPath};
            RESET ROLE;
            REVOKE auth_lookup FROM CURRENT_USER;
            """;
    }
}
