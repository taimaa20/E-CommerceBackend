using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAdjustmentLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockAdjustmentLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStock = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NewStock = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AdjustmentQuantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AdjustmentType = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustmentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLogs_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLogs_RawMaterials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentLogs_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLogs_BranchId",
                table: "StockAdjustmentLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLogs_MaterialId",
                table: "StockAdjustmentLogs",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLogs_PerformedByUserId",
                table: "StockAdjustmentLogs",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentLogs_Tenant_Branch_Material_Created",
                table: "StockAdjustmentLogs",
                columns: new[] { "TenantId", "BranchId", "MaterialId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockAdjustmentLogs");
        }
    }
}
