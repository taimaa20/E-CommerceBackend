using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddModifierRecipeOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ModifierId",
                table: "OrderItemModifiers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModifierRecipeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierRecipeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierRecipeItems_Modifiers_ModifierId",
                        column: x => x.ModifierId,
                        principalTable: "Modifiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModifierRecipeItems_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemRecipeSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawMaterialName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RawMaterialNameAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemRecipeSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItemRecipeSnapshots_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItemRecipeSnapshots_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModifierRecipeItems_ModifierId",
                table: "ModifierRecipeItems",
                column: "ModifierId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierRecipeItems_RawMaterialId",
                table: "ModifierRecipeItems",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemRecipeSnapshots_OrderItemId",
                table: "OrderItemRecipeSnapshots",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemRecipeSnapshots_RawMaterialId",
                table: "OrderItemRecipeSnapshots",
                column: "RawMaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModifierRecipeItems");

            migrationBuilder.DropTable(
                name: "OrderItemRecipeSnapshots");

            migrationBuilder.DropColumn(
                name: "ModifierId",
                table: "OrderItemModifiers");
        }
    }
}
