using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeAlternativePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CustomerAdditionalPrice",
                table: "RecipeItemAlternatives",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedOverridePrice",
                table: "RecipeItemAlternatives",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingType",
                table: "RecipeItemAlternatives",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerAdditionalPrice",
                table: "RecipeItemAlternatives");

            migrationBuilder.DropColumn(
                name: "FixedOverridePrice",
                table: "RecipeItemAlternatives");

            migrationBuilder.DropColumn(
                name: "PricingType",
                table: "RecipeItemAlternatives");
        }
    }
}
