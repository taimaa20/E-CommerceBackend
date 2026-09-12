using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseInvoiceSupplierFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_SupplierId",
                table: "ExpenseInvoices",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseInvoices_Suppliers_SupplierId",
                table: "ExpenseInvoices",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseInvoices_Suppliers_SupplierId",
                table: "ExpenseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_SupplierId",
                table: "ExpenseInvoices");
        }
    }
}
