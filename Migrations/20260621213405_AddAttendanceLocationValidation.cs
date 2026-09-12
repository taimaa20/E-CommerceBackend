using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceLocationValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CheckInDistanceMeters",
                table: "TimeEntries",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CheckInLatitude",
                table: "TimeEntries",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CheckInLongitude",
                table: "TimeEntries",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CheckOutDistanceMeters",
                table: "TimeEntries",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CheckOutLatitude",
                table: "TimeEntries",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CheckOutLongitude",
                table: "TimeEntries",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllowedRadiusMeters",
                table: "SystemSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "SystemSettings",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "SystemSettings",
                type: "numeric(18,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInDistanceMeters",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "CheckInLatitude",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "CheckInLongitude",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "CheckOutDistanceMeters",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "CheckOutLatitude",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "CheckOutLongitude",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "AllowedRadiusMeters",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "SystemSettings");
        }
    }
}
