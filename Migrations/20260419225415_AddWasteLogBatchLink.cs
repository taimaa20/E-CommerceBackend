using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWasteLogBatchLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceBatchId",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceBatchNumber",
                table: "WasteLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_SourceBatchId",
                table: "WasteLogs",
                column: "SourceBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_WasteLogs_StockBatches_SourceBatchId",
                table: "WasteLogs",
                column: "SourceBatchId",
                principalTable: "StockBatches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WasteLogs_StockBatches_SourceBatchId",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_SourceBatchId",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "SourceBatchId",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "SourceBatchNumber",
                table: "WasteLogs");
        }
    }
}
