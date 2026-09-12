using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftRulesAndAuditAndForceClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForceCloseReason",
                table: "CashierBalanceShifts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ForceClosed",
                table: "CashierBalanceShifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PosDeviceId",
                table: "CashierBalanceShifts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConfigAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedByUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PreviousValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftRulesConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveShiftRule = table.Column<int>(type: "integer", nullable: false),
                    RequireAllOrdersPaid = table.Column<bool>(type: "boolean", nullable: false),
                    RequireAllOrdersReady = table.Column<bool>(type: "boolean", nullable: false),
                    RequireAllOrdersServed = table.Column<bool>(type: "boolean", nullable: false),
                    RequireAllOrdersCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    AllowPendingOrders = table.Column<bool>(type: "boolean", nullable: false),
                    AllowPreparingOrders = table.Column<bool>(type: "boolean", nullable: false),
                    AllowReadyOrders = table.Column<bool>(type: "boolean", nullable: false),
                    AllowPendingDeliveryOrders = table.Column<bool>(type: "boolean", nullable: false),
                    AllowPendingCancellationRequests = table.Column<bool>(type: "boolean", nullable: false),
                    AllowForcedShiftClose = table.Column<bool>(type: "boolean", nullable: false),
                    AllowShiftCloseWithOpenOrders = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftRulesConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigAuditLogs_Tenant_CreatedAt",
                table: "ConfigAuditLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigAuditLogs_Tenant_EventType_CreatedAt",
                table: "ConfigAuditLogs",
                columns: new[] { "TenantId", "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ShiftRulesConfigs_Tenant",
                table: "ShiftRulesConfigs",
                column: "TenantId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigAuditLogs");

            migrationBuilder.DropTable(
                name: "ShiftRulesConfigs");

            migrationBuilder.DropColumn(
                name: "ForceCloseReason",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "ForceClosed",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "PosDeviceId",
                table: "CashierBalanceShifts");
        }
    }
}
