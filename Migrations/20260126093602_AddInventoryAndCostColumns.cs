using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndCostColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("4ab7c9f9-ec04-402a-8a1c-9a1af7251bbe"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("71b05e0d-504a-4db4-9b1f-ff62e36ac409"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("dfda5b3f-ffe5-44c8-8396-f6af3f9073e0"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f71e14d8-b8cd-43f7-a544-b16b392652c0"));

            migrationBuilder.AddColumn<decimal>(
                name: "NetProfit",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InitialQuantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockLots_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockLots_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("470dc8d4-529a-4352-8f73-1d168cec805f"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("4a7d21a5-6d60-49fd-9dac-57538a0bed75"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("a5b8c4bb-54be-4d84-9c41-e12959f71466"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("b07acb6b-073a-4a83-b747-eea2b1d45364"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_PurchaseOrderId",
                table: "StockLots",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLots_RawMaterialId",
                table: "StockLots",
                column: "RawMaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockLots");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("470dc8d4-529a-4352-8f73-1d168cec805f"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("4a7d21a5-6d60-49fd-9dac-57538a0bed75"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a5b8c4bb-54be-4d84-9c41-e12959f71466"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b07acb6b-073a-4a83-b747-eea2b1d45364"));

            migrationBuilder.DropColumn(
                name: "NetProfit",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "Orders");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("4ab7c9f9-ec04-402a-8a1c-9a1af7251bbe"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("71b05e0d-504a-4db4-9b1f-ff62e36ac409"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("dfda5b3f-ffe5-44c8-8396-f6af3f9073e0"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("f71e14d8-b8cd-43f7-a544-b16b392652c0"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" }
                });
        }
    }
}
