using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddManualWasteLogModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "WasteLogs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "WasteLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostAmount",
                table: "WasteLogs",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "WasteLogs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionAt",
                table: "WasteLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAffectingInventory",
                table: "WasteLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MaterialId",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "WasteLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePriceLoss",
                table: "WasteLogs",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "WasteLogs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "WasteNumber",
                table: "WasteLogs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WasteType",
                table: "WasteLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<int>(type: "integer", nullable: false),
                    WasteLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CostAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_RawMaterials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_WasteLogs_WasteLogId",
                        column: x => x.WasteLogId,
                        principalTable: "WasteLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WasteLogAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WasteLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    PerformedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteLogAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WasteLogAudits_Users_PerformedById",
                        column: x => x.PerformedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WasteLogAudits_WasteLogs_WasteLogId",
                        column: x => x.WasteLogId,
                        principalTable: "WasteLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_ApprovedById",
                table: "WasteLogs",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_MaterialId",
                table: "WasteLogs",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_ProductId",
                table: "WasteLogs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_LoggedById_CreatedAt",
                table: "WasteLogs",
                columns: new[] { "TenantId", "LoggedById", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_ProductId_CreatedAt",
                table: "WasteLogs",
                columns: new[] { "TenantId", "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_Status_CreatedAt",
                table: "WasteLogs",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_WasteNumber",
                table: "WasteLogs",
                columns: new[] { "TenantId", "WasteNumber" },
                unique: true,
                filter: "\"WasteNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_WasteType_CreatedAt",
                table: "WasteLogs",
                columns: new[] { "TenantId", "WasteType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_MaterialId",
                table: "InventoryTransactions",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductId",
                table: "InventoryTransactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_TransactionType_CreatedAt",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "TransactionType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_WasteLogId",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "WasteLogId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_WasteLogId",
                table: "InventoryTransactions",
                column: "WasteLogId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogAudits_PerformedById",
                table: "WasteLogAudits",
                column: "PerformedById");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogAudits_TenantId_WasteLogId_CreatedAt",
                table: "WasteLogAudits",
                columns: new[] { "TenantId", "WasteLogId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogAudits_WasteLogId",
                table: "WasteLogAudits",
                column: "WasteLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_WasteLogs_Products_ProductId",
                table: "WasteLogs",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WasteLogs_RawMaterials_MaterialId",
                table: "WasteLogs",
                column: "MaterialId",
                principalTable: "RawMaterials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WasteLogs_Users_ApprovedById",
                table: "WasteLogs",
                column: "ApprovedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WasteLogs_Products_ProductId",
                table: "WasteLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_WasteLogs_RawMaterials_MaterialId",
                table: "WasteLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_WasteLogs_Users_ApprovedById",
                table: "WasteLogs");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "WasteLogAudits");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_ApprovedById",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_MaterialId",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_ProductId",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_TenantId_LoggedById_CreatedAt",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_TenantId_ProductId_CreatedAt",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_TenantId_Status_CreatedAt",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_TenantId_WasteNumber",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_TenantId_WasteType_CreatedAt",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "CostAmount",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "IsAffectingInventory",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "MaterialId",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "SalePriceLoss",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "WasteNumber",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "WasteType",
                table: "WasteLogs");
        }
    }
}
