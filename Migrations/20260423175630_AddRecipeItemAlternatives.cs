using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeItemAlternatives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceAlternativeId",
                table: "OrderItemRecipeSnapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAlternativeName",
                table: "OrderItemRecipeSnapshots",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAlternativeNameAr",
                table: "OrderItemRecipeSnapshots",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRecipeItemId",
                table: "OrderItemRecipeSnapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecipeItemAlternatives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PriceAdjustment = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeItemAlternatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeItemAlternatives_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeItemAlternatives_RecipeItems_RecipeItemId",
                        column: x => x.RecipeItemId,
                        principalTable: "RecipeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeItemAlternatives_RawMaterialId",
                table: "RecipeItemAlternatives",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeItemAlternatives_RecipeItemId",
                table: "RecipeItemAlternatives",
                column: "RecipeItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeItemAlternatives");

            migrationBuilder.DropColumn(
                name: "SourceAlternativeId",
                table: "OrderItemRecipeSnapshots");

            migrationBuilder.DropColumn(
                name: "SourceAlternativeName",
                table: "OrderItemRecipeSnapshots");

            migrationBuilder.DropColumn(
                name: "SourceAlternativeNameAr",
                table: "OrderItemRecipeSnapshots");

            migrationBuilder.DropColumn(
                name: "SourceRecipeItemId",
                table: "OrderItemRecipeSnapshots");
        }
    }
}
