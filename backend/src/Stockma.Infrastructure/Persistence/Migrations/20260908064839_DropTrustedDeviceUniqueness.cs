using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropTrustedDeviceUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_trusted_devices_tenant_user_device",
                table: "trusted_devices");

            migrationBuilder.CreateIndex(
                name: "ix_trusted_devices_tenant_user_device",
                table: "trusted_devices",
                columns: new[] { "tenant_id", "user_id", "device_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_trusted_devices_tenant_user_device",
                table: "trusted_devices");

            migrationBuilder.CreateIndex(
                name: "ux_trusted_devices_tenant_user_device",
                table: "trusted_devices",
                columns: new[] { "tenant_id", "user_id", "device_id" },
                unique: true);
        }
    }
}
