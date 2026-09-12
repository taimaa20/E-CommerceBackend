using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddModifierLinkedProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedProductId",
                table: "Modifiers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modifiers_LinkedProductId",
                table: "Modifiers",
                column: "LinkedProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Modifiers_Products_LinkedProductId",
                table: "Modifiers",
                column: "LinkedProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Modifiers_Products_LinkedProductId",
                table: "Modifiers");

            migrationBuilder.DropIndex(
                name: "IX_Modifiers_LinkedProductId",
                table: "Modifiers");

            migrationBuilder.DropColumn(
                name: "LinkedProductId",
                table: "Modifiers");
        }
    }
}
