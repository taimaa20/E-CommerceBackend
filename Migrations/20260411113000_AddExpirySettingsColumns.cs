using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExpirySettingsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpiryJobRunTime",
                table: "SystemSettings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "01:00");

            migrationBuilder.AddColumn<string>(
                name: "ExpiryNotifyRoles",
                table: "SystemSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Admin,Manager");

            migrationBuilder.AddColumn<int>(
                name: "NearExpiryDays",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiryJobRunTime",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "ExpiryNotifyRoles",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "NearExpiryDays",
                table: "SystemSettings");
        }
    }
}
