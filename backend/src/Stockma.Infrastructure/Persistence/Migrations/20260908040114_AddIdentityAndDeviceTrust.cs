using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAndDeviceTrust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_claims_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_otps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_otps", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_otps_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_device_otps_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trusted_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    trusted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trusted_devices", x => x.id);
                    table.ForeignKey(
                        name: "FK_trusted_devices_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trusted_devices_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "FK_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_otps_user_id",
                table: "device_otps",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_otps_tenant_user_device_expiration",
                table: "device_otps",
                columns: new[] { "tenant_id", "user_id", "device_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_role_claims_role_id",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_roles_normalized_name",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trusted_devices_user_id",
                table: "trusted_devices",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_trusted_devices_tenant_user_device",
                table: "trusted_devices",
                columns: new[] { "tenant_id", "user_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_user_id",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id",
                table: "users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_user_name",
                table: "users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.Sql("""
                -- Mismo criterio fail-closed que InitialSchema: sin app.tenant no se
                -- devuelve ninguna fila.
                --
                -- OJO con `users`: el login corre SIN contexto de tenant (ADR-002), asi que
                -- esta politica lo bloquea. IgnoreQueryFilters() esquiva el filtro de EF,
                -- NO la RLS. La busqueda por email del login DEBE pasar por
                -- auth_find_user_by_email (abajo), que es la unica excepcion y esta acotada
                -- a las columnas de autenticacion.
                ALTER TABLE users ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON users
                    USING (tenant_id = NULLIF(current_setting('app.tenant', true), '')::uuid);

                ALTER TABLE trusted_devices ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON trusted_devices
                    USING (tenant_id = NULLIF(current_setting('app.tenant', true), '')::uuid);

                ALTER TABLE device_otps ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON device_otps
                    USING (tenant_id = NULLIF(current_setting('app.tenant', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                -- UNICO punto del sistema que cruza la frontera entre tenants a proposito
                -- (T055b). SECURITY DEFINER corre como el propietario de la tabla, para el
                -- que PostgreSQL no aplica RLS.
                --
                -- La excepcion esta acotada por construccion:
                --   * devuelve como maximo UNA fila, la del email exacto;
                --   * devuelve SOLO las columnas necesarias para autenticar y resolver el
                --     tenant. No expone telefono, ni lockout, ni nada mas;
                --   * no acepta filtros arbitrarios: la firma es un unico email normalizado.
                --
                -- El filtro global NO DEBE volverse permisivo con app.tenant vacio: la
                -- excepcion vive aca, en el punto de uso, y no en la politica.
                CREATE OR REPLACE FUNCTION auth_find_user_by_email(p_normalized_email text)
                RETURNS TABLE (
                    id uuid,
                    tenant_id uuid,
                    password_hash text,
                    security_stamp text,
                    lockout_end timestamptz,
                    lockout_enabled boolean
                )
                LANGUAGE sql
                SECURITY DEFINER
                SET search_path = public
                AS $$
                    SELECT u.id, u.tenant_id, u.password_hash, u.security_stamp,
                           u.lockout_end, u.lockout_enabled
                    FROM users u
                    WHERE u.normalized_email = p_normalized_email
                    LIMIT 1;
                $$;

                REVOKE ALL ON FUNCTION auth_find_user_by_email(text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION auth_find_user_by_email(text) TO app_user;
                """);

            migrationBuilder.Sql("""
                -- Roles del tenant (T062). Se siembran aca para que no exista un tenant sin
                -- roles disponibles al crear su primer usuario.
                INSERT INTO roles (id, name, normalized_name, concurrency_stamp)
                VALUES
                    ('0199a1b2-0001-7000-8000-000000000001', 'TenantAdmin', 'TENANTADMIN', gen_random_uuid()::text),
                    ('0199a1b2-0001-7000-8000-000000000002', 'Member', 'MEMBER', gen_random_uuid()::text)
                ON CONFLICT (normalized_name) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS auth_find_user_by_email(text);
                DROP POLICY IF EXISTS tenant_isolation ON device_otps;
                ALTER TABLE device_otps DISABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON trusted_devices;
                ALTER TABLE trusted_devices DISABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON users;
                ALTER TABLE users DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "device_otps");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "trusted_devices");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
