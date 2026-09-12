using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShiftReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByUserId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardOrdersTotal",
                table: "CashierBalanceShifts",
                type: "numeric(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashOrdersTotal",
                table: "CashierBalanceShifts",
                type: "numeric(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosingComment",
                table: "CashierBalanceShifts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedBalance",
                table: "CashierBalanceShifts",
                type: "numeric(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrdersTotal",
                table: "CashierBalanceShifts",
                type: "numeric(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaidOrderCount",
                table: "CashierBalanceShifts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaidByUserId",
                table: "Orders",
                column: "PaidByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_PaidByUserId_PaidAt",
                table: "Orders",
                columns: new[] { "TenantId", "PaidByUserId", "PaidAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_PaidByUserId",
                table: "Orders",
                column: "PaidByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_PaidByUserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaidByUserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_PaidByUserId_PaidAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaidByUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CardOrdersTotal",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "CashOrdersTotal",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "ClosingComment",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "ExpectedBalance",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "OrdersTotal",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "PaidOrderCount",
                table: "CashierBalanceShifts");
        }
    }
}
