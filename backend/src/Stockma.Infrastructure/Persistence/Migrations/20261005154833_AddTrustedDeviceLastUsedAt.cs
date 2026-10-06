using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stockma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustedDeviceLastUsedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se agrega nullable sólo para poder backfillear desde trusted_at: el propio alta de un
            // dispositivo ya es un uso (hubo OTP en esa máquina), así que trusted_at es el piso
            // verdadero del último uso y ninguna fila puede quedar en NULL.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_used_at",
                table: "trusted_devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE trusted_devices SET last_used_at = trusted_at WHERE last_used_at IS NULL;");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "last_used_at",
                table: "trusted_devices",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_used_at",
                table: "trusted_devices");
        }
    }
}
