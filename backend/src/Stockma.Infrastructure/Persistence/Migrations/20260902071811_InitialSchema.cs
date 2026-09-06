using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    active_ingredient = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    presentation = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    storage_conditions = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_settings",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_trusted_devices = table.Column<int>(type: "integer", nullable: false),
                    green_months = table.Column<int>(type: "integer", nullable: false),
                    yellow_months = table.Column<int>(type: "integer", nullable: false),
                    next_sku_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_settings", x => x.tenant_id);
                    table.ForeignKey(
                        name: "FK_tenant_settings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: false),
                    current_quantity = table.Column<int>(type: "integer", nullable: false),
                    location_shelf = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_batches_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_batches_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_batches_tenant_id",
                table: "batches",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_batches_product_expiration",
                table: "batches",
                columns: new[] { "product_id", "expiration_date" });

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_barcode",
                table: "products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_sku",
                table: "products",
                columns: new[] { "tenant_id", "sku" },
                unique: true);
            migrationBuilder.Sql("""
                -- Rol de aplicacion NO propietario. Es imprescindible que no sea el
                -- dueno de las tablas: PostgreSQL no aplica politicas RLS al propietario,
                -- asi que conectarse con el rol propietario dejaria la RLS decorativa.
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'app_user') THEN
                        CREATE ROLE app_user LOGIN;
                    END IF;
                END
                $$;

                GRANT USAGE ON SCHEMA public TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public
                    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;
                """);

            migrationBuilder.Sql("""
                -- current_setting(..., true) devuelve NULL en vez de fallar cuando la
                -- variable no esta definida, y NULLIF cubre el caso de cadena vacia.
                -- Con NULL la comparacion nunca es verdadera: sin tenant activo no se
                -- devuelve ninguna fila (fail-closed).
                ALTER TABLE tenant_settings ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tenant_settings
                    USING (tenant_id = NULLIF(current_setting('app.tenant', true), '')::uuid);

                ALTER TABLE products ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON products
                    USING (tenant_id = NULLIF(current_setting('app.tenant', true), '')::uuid);

                ALTER TABLE batches ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON batches
                    USING (tenant_id = NULLIF(current_setting('app.tenant', true), '')::uuid);
                """);
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pg_trgm;

                CREATE INDEX ix_products_name_trgm
                    ON products USING gin (name gin_trgm_ops);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_products_name_trgm;
                DROP POLICY IF EXISTS tenant_isolation ON batches;
                ALTER TABLE batches DISABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON products;
                ALTER TABLE products DISABLE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS tenant_isolation ON tenant_settings;
                ALTER TABLE tenant_settings DISABLE ROW LEVEL SECURITY;
                """);
            migrationBuilder.DropTable(
                name: "batches");

            migrationBuilder.DropTable(
                name: "tenant_settings");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
