using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineSyncHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientActionId",
                table: "Payments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientOrderUuid",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicOrderNumber",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloseTransactionId",
                table: "CashierBalanceShifts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenTransactionId",
                table: "CashierBalanceShifts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IdempotencyEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ContentType = table.Column<string>(type: "text", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessingExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Tenant_ClientActionId",
                table: "Payments",
                columns: new[] { "TenantId", "ClientActionId" },
                unique: true,
                filter: "\"ClientActionId\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_ClientOrderUuid",
                table: "Orders",
                columns: new[] { "TenantId", "ClientOrderUuid" },
                unique: true,
                filter: "\"ClientOrderUuid\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_PublicOrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "PublicOrderNumber" },
                filter: "\"PublicOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashierBalanceShifts_Tenant_CloseTx",
                table: "CashierBalanceShifts",
                columns: new[] { "TenantId", "CloseTransactionId" },
                unique: true,
                filter: "\"CloseTransactionId\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashierBalanceShifts_Tenant_OpenTx",
                table: "CashierBalanceShifts",
                columns: new[] { "TenantId", "OpenTransactionId" },
                unique: true,
                filter: "\"OpenTransactionId\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntries_Tenant_Key",
                table: "IdempotencyEntries",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyEntries_Tenant_State_Expires",
                table: "IdempotencyEntries",
                columns: new[] { "TenantId", "State", "ProcessingExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyEntries");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Tenant_ClientActionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_ClientOrderUuid",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_PublicOrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_CashierBalanceShifts_Tenant_CloseTx",
                table: "CashierBalanceShifts");

            migrationBuilder.DropIndex(
                name: "IX_CashierBalanceShifts_Tenant_OpenTx",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "ClientActionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ClientOrderUuid",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PublicOrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CloseTransactionId",
                table: "CashierBalanceShifts");

            migrationBuilder.DropColumn(
                name: "OpenTransactionId",
                table: "CashierBalanceShifts");
        }
    }
}
