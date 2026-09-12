using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAccountingBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualDeliveryCost",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerDeliveryFee",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryMargin",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodSubtotal",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketplaceDeliveryFee",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketplaceServiceFee",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetRestaurantRevenue",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDeliveryCostValue",
                table: "DeliveryPartners",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryCostRuleType",
                table: "DeliveryPartners",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualDeliveryCost",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerDeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryMargin",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FoodSubtotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MarketplaceDeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MarketplaceServiceFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "NetRestaurantRevenue",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DefaultDeliveryCostValue",
                table: "DeliveryPartners");

            migrationBuilder.DropColumn(
                name: "DeliveryCostRuleType",
                table: "DeliveryPartners");
        }
    }
}
