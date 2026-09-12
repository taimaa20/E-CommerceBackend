using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShiftHistorySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NetTotal",
                table: "CashierBalanceShifts",
                type: "numeric(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundsTotal",
                table: "CashierBalanceShifts",
                type: "numeric(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShiftNumber",
                table: "CashierBalanceShifts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierBalanceShifts_Tenant_Cashier_OpenedAt",
                table: "CashierBalanceShifts",
                columns: new[] { "TenantId", "CashierId", "OpenedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashierBalanceShifts_Tenant_Cashier_OpenedAt",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "NetTotal",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "RefundsTotal",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "ShiftNumber",
                table: "CashierBalanceShifts");
        }
    }
}
