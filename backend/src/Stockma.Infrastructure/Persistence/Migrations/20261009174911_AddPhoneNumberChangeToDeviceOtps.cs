using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneNumberChangeToDeviceOtps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "new_phone_number",
                table: "device_otps",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "new_phone_number",
                table: "device_otps");
        }
    }
}
