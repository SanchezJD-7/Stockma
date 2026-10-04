using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustedDeviceLifetimeDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "trusted_device_lifetime_days",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trusted_device_lifetime_days",
                table: "tenant_settings");
        }
    }
}
