using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RideMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LocationPresenceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BatteryLevel",
                table: "LocationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocationPermissionDenied",
                table: "LocationLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryLevel",
                table: "LocationLogs");

            migrationBuilder.DropColumn(
                name: "IsLocationPermissionDenied",
                table: "LocationLogs");
        }
    }
}
