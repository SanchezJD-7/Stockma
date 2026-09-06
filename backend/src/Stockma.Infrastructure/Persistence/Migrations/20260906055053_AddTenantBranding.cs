using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branding_primary",
                table: "tenant_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "branding_primary_active",
                table: "tenant_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "branding_primary_bg",
                table: "tenant_settings",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "branding_primary",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "branding_primary_active",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "branding_primary_bg",
                table: "tenant_settings");
        }
    }
}
