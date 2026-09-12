using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShiftExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "ExpenseInvoices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "ExpenseInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledById",
                table: "ExpenseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByName",
                table: "ExpenseInvoices",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CashierShiftId",
                table: "ExpenseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                table: "ExpenseInvoices",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentMethodId",
                table: "ExpenseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "ExpenseInvoiceAuditLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_CancelledById",
                table: "ExpenseInvoices",
                column: "CancelledById");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_CashierShiftId",
                table: "ExpenseInvoices",
                column: "CashierShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_PaymentMethodId",
                table: "ExpenseInvoices",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Tenant_Branch_Shift_Status",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "BranchId", "CashierShiftId", "Status" },
                filter: "\"CashierShiftId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseInvoices_CashierBalanceShifts_CashierShiftId",
                table: "ExpenseInvoices",
                column: "CashierShiftId",
                principalTable: "CashierBalanceShifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseInvoices_PaymentMethods_PaymentMethodId",
                table: "ExpenseInvoices",
                column: "PaymentMethodId",
                principalTable: "PaymentMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseInvoices_Users_CancelledById",
                table: "ExpenseInvoices",
                column: "CancelledById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseInvoices_CashierBalanceShifts_CashierShiftId",
                table: "ExpenseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseInvoices_PaymentMethods_PaymentMethodId",
                table: "ExpenseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseInvoices_Users_CancelledById",
                table: "ExpenseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_CancelledById",
                table: "ExpenseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_CashierShiftId",
                table: "ExpenseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_PaymentMethodId",
                table: "ExpenseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_Tenant_Branch_Shift_Status",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "CancelledById",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "CancelledByName",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "CashierShiftId",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "CreatedByName",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentMethodId",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "ExpenseInvoiceAuditLogs");
        }
    }
}
