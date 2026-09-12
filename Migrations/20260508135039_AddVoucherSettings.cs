using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VoucherAmount",
                table: "SystemSettings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 300m);

            migrationBuilder.AddColumn<int>(
                name: "VoucherDailyLimit",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<bool>(
                name: "VoucherEnabled",
                table: "SystemSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoucherAmount",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "VoucherDailyLimit",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "VoucherEnabled",
                table: "SystemSettings");
        }
    }
}
