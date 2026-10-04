using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "refresh_token_lifetime_hours",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<int>(
                name: "session_idle_timeout_minutes",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    family_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_tenant_family",
                table: "refresh_tokens",
                columns: new[] { "tenant_id", "family_id" });

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.Sql("""
                -- Mismo criterio fail-closed que el resto de las tablas de tenant (ADR-017):
                -- sin app.tenant no se devuelve ninguna fila, y FORCE hace que la politica
                -- tambien le aplique al propietario de la tabla.
                ALTER TABLE refresh_tokens ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON refresh_tokens
                    USING (tenant_id = NULLIF(current_setting('app.tenant', true), '')::uuid);
                ALTER TABLE refresh_tokens FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.Sql("""
                -- `refresh` y `logout` corren sin `app.tenant` (T065, ADR-018): el tenant y el
                -- usuario se resuelven desde la fila del refresh token, por la MISMA receta que
                -- auth_find_user_by_email (ADR-017) — auth_lookup, sin login, sin tablas propias,
                -- SELECT acotado y una politica de lectura que aplica SOLO a ese rol. A diferencia
                -- de aquella funcion, esta se crea ya con el search_path endurecido (pg_temp al
                -- final) desde el arranque: no hace falta una migracion de endurecimiento aparte.
                --
                -- La excepcion esta acotada por construccion:
                --   * devuelve como maximo UNA fila, la del hash exacto;
                --   * NO devuelve token_hash: solo lo necesario para que 2c resuelva tenant,
                --     usuario, familia, dispositivo y las marcas de vencimiento/consumo/revocacion;
                --   * no acepta filtros arbitrarios: la firma es un unico hash.
                DO $$
                BEGIN
                    IF NOT pg_has_role(current_user, 'auth_lookup', 'USAGE WITH ADMIN OPTION') THEN
                        RAISE EXCEPTION 'El rol que migra (%) no tiene ADMIN sobre auth_lookup, que ya existe en el cluster y lo creo otro rol. Un superusuario tiene que correr: GRANT auth_lookup TO % WITH ADMIN OPTION, INHERIT FALSE, SET FALSE; y despues se vuelve a migrar. Ver ADR-017.',
                            current_user, quote_ident(current_user);
                    END IF;
                END
                $$;

                GRANT auth_lookup TO CURRENT_USER WITH INHERIT FALSE, SET TRUE;
                GRANT CREATE ON SCHEMA public TO auth_lookup;

                SET ROLE auth_lookup;
                CREATE FUNCTION auth_find_refresh_token_by_hash(p_token_hash text)
                RETURNS TABLE (
                    id uuid,
                    tenant_id uuid,
                    user_id uuid,
                    family_id uuid,
                    device_id character varying(128),
                    expires_at timestamptz,
                    family_expires_at timestamptz,
                    consumed_at timestamptz,
                    revoked_at timestamptz
                )
                LANGUAGE sql
                SECURITY DEFINER
                SET search_path = pg_catalog, public, pg_temp
                AS $func$
                    SELECT r.id, r.tenant_id, r.user_id, r.family_id, r.device_id,
                           r.expires_at, r.family_expires_at, r.consumed_at, r.revoked_at
                    FROM refresh_tokens r
                    WHERE r.token_hash = p_token_hash
                    LIMIT 1;
                $func$;
                RESET ROLE;

                REVOKE CREATE ON SCHEMA public FROM auth_lookup;
                REVOKE auth_lookup FROM CURRENT_USER;

                GRANT SELECT (token_hash, id, tenant_id, user_id, family_id, device_id,
                              expires_at, family_expires_at, consumed_at, revoked_at)
                    ON refresh_tokens TO auth_lookup;

                CREATE POLICY auth_lookup_read ON refresh_tokens
                    FOR SELECT TO auth_lookup
                    USING (true);

                REVOKE ALL ON FUNCTION auth_find_refresh_token_by_hash(text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION auth_find_refresh_token_by_hash(text) TO app_user;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                DROP FUNCTION IF EXISTS auth_find_refresh_token_by_hash(text);
                RESET ROLE;
                REVOKE auth_lookup FROM CURRENT_USER;

                DROP POLICY IF EXISTS auth_lookup_read ON refresh_tokens;
                REVOKE ALL ON refresh_tokens FROM auth_lookup;

                DROP POLICY IF EXISTS tenant_isolation ON refresh_tokens;
                ALTER TABLE refresh_tokens NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE refresh_tokens DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "refresh_token_lifetime_hours",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "session_idle_timeout_minutes",
                table: "tenant_settings");
        }
    }
}
