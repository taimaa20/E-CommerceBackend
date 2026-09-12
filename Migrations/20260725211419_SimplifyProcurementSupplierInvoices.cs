using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyProcurementSupplierInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "ExpenseInvoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderId",
                table: "ExpenseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_PurchaseOrder",
                table: "ExpenseInvoices",
                column: "PurchaseOrderId",
                unique: true,
                filter: "\"PurchaseOrderId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseInvoices_PurchaseOrders_PurchaseOrderId",
                table: "ExpenseInvoices",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseInvoices_PurchaseOrders_PurchaseOrderId",
                table: "ExpenseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_PurchaseOrder",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "ExpenseInvoices");
        }
    }
}
