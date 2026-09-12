using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRetailStockLedgerAndPurchasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetailPurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OrderDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockPostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShippingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CustomsCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClearanceCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LandedCostAllocation = table.Column<int>(type: "integer", nullable: false),
                    GoodsCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailPurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetailPurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetailStockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    SourceDocumentType = table.Column<int>(type: "integer", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedBySystem = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ReversesMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailStockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetailStockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetailPurchaseOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RetailPurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkuSnapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuantityOrdered = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AllocatedCharges = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LandedUnitCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailPurchaseOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetailPurchaseOrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetailPurchaseOrderLines_RetailPurchaseOrders_RetailPurchas~",
                        column: x => x.RetailPurchaseOrderId,
                        principalTable: "RetailPurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetailPurchaseOrderLines_ProductId",
                table: "RetailPurchaseOrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailPurchaseOrderLines_RetailPurchaseOrderId",
                table: "RetailPurchaseOrderLines",
                column: "RetailPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailPurchaseOrderLines_Tenant_Order",
                table: "RetailPurchaseOrderLines",
                columns: new[] { "TenantId", "RetailPurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailPurchaseOrderLines_Tenant_Product",
                table: "RetailPurchaseOrderLines",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailPurchaseOrders_SupplierId",
                table: "RetailPurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailPurchaseOrders_Tenant_Branch_Status_Date",
                table: "RetailPurchaseOrders",
                columns: new[] { "TenantId", "BranchId", "Status", "OrderDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailPurchaseOrders_Tenant_Number",
                table: "RetailPurchaseOrders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetailPurchaseOrders_Tenant_Supplier",
                table: "RetailPurchaseOrders",
                columns: new[] { "TenantId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailStockMovements_Line_Once",
                table: "RetailStockMovements",
                columns: new[] { "TenantId", "MovementType", "SourceLineId" },
                unique: true,
                filter: "\"SourceLineId\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetailStockMovements_Opening_Once",
                table: "RetailStockMovements",
                columns: new[] { "TenantId", "BranchId", "ProductId", "MovementType" },
                unique: true,
                filter: "\"MovementType\" = 0 AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetailStockMovements_ProductId",
                table: "RetailStockMovements",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailStockMovements_Tenant_Branch_Product",
                table: "RetailStockMovements",
                columns: new[] { "TenantId", "BranchId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailStockMovements_Tenant_OccurredAt",
                table: "RetailStockMovements",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailStockMovements_Tenant_SourceDocument",
                table: "RetailStockMovements",
                columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetailPurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "RetailStockMovements");

            migrationBuilder.DropTable(
                name: "RetailPurchaseOrders");
        }
    }
}
