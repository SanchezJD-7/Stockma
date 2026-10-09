using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SetSessionIdleTimeoutDefaultTo30 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "session_idle_timeout_minutes",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 30,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 120);

            migrationBuilder.Sql(
                "UPDATE tenant_settings SET session_idle_timeout_minutes = 30 WHERE session_idle_timeout_minutes = 120;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "session_idle_timeout_minutes",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 120,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 30);

            migrationBuilder.Sql(
                "UPDATE tenant_settings SET session_idle_timeout_minutes = 120 WHERE session_idle_timeout_minutes = 30;");
        }
    }
}
