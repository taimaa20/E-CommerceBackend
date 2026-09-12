using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRetailLegacyPurchasesAndSupplierBestSellers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BestSellers",
                table: "Suppliers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RetailLegacyPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PurchaseOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrderDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QuantityPieces = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SupplierCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShippingCustomsClearance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SourceRowNumber = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetailLegacyPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetailLegacyPurchases_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetailLegacyPurchases_SupplierId",
                table: "RetailLegacyPurchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_RetailLegacyPurchases_Tenant_Branch_Date",
                table: "RetailLegacyPurchases",
                columns: new[] { "TenantId", "BranchId", "OrderDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RetailLegacyPurchases_Tenant_Number",
                table: "RetailLegacyPurchases",
                columns: new[] { "TenantId", "PurchaseOrderNumber" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetailLegacyPurchases");

            migrationBuilder.DropColumn(
                name: "BestSellers",
                table: "Suppliers");
        }
    }
}
