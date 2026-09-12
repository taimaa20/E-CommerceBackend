using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "SupplierName",
                table: "PurchaseOrders");

            migrationBuilder.RenameColumn(
                name: "PurchaseDate",
                table: "PurchaseOrders",
                newName: "ExpectedDate");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "PurchaseOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                table: "PurchaseOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedDate",
                table: "PurchaseOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PurchaseOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "PurchaseOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VendorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactInfo = table.Column<string>(type: "text", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("0e941863-d49d-4f1c-a324-bfb53f8e45f7"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("1e521484-03b3-4db8-97b5-964750a74546"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("d066b637-a03f-413e-8241-34e0a11cee5c"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("dd551dd2-9a5a-47c8-91f1-00b8f456133c"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_RawMaterialId",
                table: "PurchaseOrderItems",
                column: "RawMaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("0e941863-d49d-4f1c-a324-bfb53f8e45f7"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("1e521484-03b3-4db8-97b5-964750a74546"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d066b637-a03f-413e-8241-34e0a11cee5c"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("dd551dd2-9a5a-47c8-91f1-00b8f456133c"));

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReceivedDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.RenameColumn(
                name: "ExpectedDate",
                table: "PurchaseOrders",
                newName: "PurchaseDate");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "PurchaseOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "PurchaseOrders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

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
        }
    }
}
