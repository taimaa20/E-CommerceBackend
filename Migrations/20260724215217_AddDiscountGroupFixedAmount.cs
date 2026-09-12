using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountGroupFixedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountGroupPercentage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "DiscountGroups");

            migrationBuilder.AddColumn<int>(
                name: "DiscountGroupType",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountGroupValue",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountType",
                table: "DiscountGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "DiscountGroups",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountGroupType",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountGroupValue",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "DiscountGroups");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "DiscountGroups");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountGroupPercentage",
                table: "Orders",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "DiscountGroups",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
