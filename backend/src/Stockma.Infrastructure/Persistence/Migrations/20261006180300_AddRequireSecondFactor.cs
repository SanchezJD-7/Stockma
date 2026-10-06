using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequireSecondFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_second_factor",
                table: "tenant_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "require_second_factor",
                table: "tenant_settings");
        }
    }
}
