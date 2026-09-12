using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_StaffId",
                table: "TimeEntries");

            migrationBuilder.AddColumn<string>(
                name: "CheckInNote",
                table: "TimeEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckOutNote",
                table: "TimeEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "TimeEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_StaffId_ClockIn",
                table: "TimeEntries",
                columns: new[] { "StaffId", "ClockIn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_StaffId_ClockIn",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "CheckInNote",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "CheckOutNote",
                table: "TimeEntries");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "TimeEntries");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_StaffId",
                table: "TimeEntries",
                column: "StaffId");
        }
    }
}
