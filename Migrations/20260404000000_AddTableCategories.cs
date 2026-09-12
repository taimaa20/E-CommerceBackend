using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTableCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TableCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableCategories", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "TableCategoryId",
                table: "Tables",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_TableCategoryId",
                table: "Tables",
                column: "TableCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TableCategories_TenantId",
                table: "TableCategories",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_TableCategories_TableCategoryId",
                table: "Tables",
                column: "TableCategoryId",
                principalTable: "TableCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tables_TableCategories_TableCategoryId",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_TableCategoryId",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "TableCategoryId",
                table: "Tables");

            migrationBuilder.DropTable(
                name: "TableCategories");
        }
    }
}
