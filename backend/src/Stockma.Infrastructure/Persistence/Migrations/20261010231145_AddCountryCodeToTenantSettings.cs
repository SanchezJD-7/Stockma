using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    public partial class AddCountryCodeToTenantSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "tenant_settings",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true,
                defaultValue: "CO");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "country_code",
                table: "tenant_settings");
        }
    }
}
