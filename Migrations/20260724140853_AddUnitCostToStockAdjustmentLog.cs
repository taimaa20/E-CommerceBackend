using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitCostToStockAdjustmentLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "StockAdjustmentLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "NewUnitCost",
                table: "StockAdjustmentLogs",
                type: "numeric(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviousUnitCost",
                table: "StockAdjustmentLogs",
                type: "numeric(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "StockAdjustmentLogs");

            migrationBuilder.DropColumn(
                name: "NewUnitCost",
                table: "StockAdjustmentLogs");

            migrationBuilder.DropColumn(
                name: "PreviousUnitCost",
                table: "StockAdjustmentLogs");
        }
    }
}
