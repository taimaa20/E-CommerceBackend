using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPosLogShiftTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "WasteLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "WasteLogs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemId",
                table: "CancelLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemName",
                table: "CancelLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "CancelLogs",
                type: "numeric(10,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "CancelLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6193), new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6193) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6180), new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6184) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6196), new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6196) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6195), new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6195) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6198), new DateTime(2026, 5, 1, 19, 59, 1, 227, DateTimeKind.Utc).AddTicks(6198) });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_ShiftId",
                table: "WasteLogs",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_Type_ShiftId_CreatedAt",
                table: "WasteLogs",
                columns: new[] { "TenantId", "Type", "ShiftId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CancelLogs_ShiftId",
                table: "CancelLogs",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CancelLogs_TenantId_ShiftId_CancelledAt",
                table: "CancelLogs",
                columns: new[] { "TenantId", "ShiftId", "CancelledAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_CancelLogs_CashierBalanceShifts_ShiftId",
                table: "CancelLogs",
                column: "ShiftId",
                principalTable: "CashierBalanceShifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WasteLogs_CashierBalanceShifts_ShiftId",
                table: "WasteLogs",
                column: "ShiftId",
                principalTable: "CashierBalanceShifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CancelLogs_CashierBalanceShifts_ShiftId",
                table: "CancelLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_WasteLogs_CashierBalanceShifts_ShiftId",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_ShiftId",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_TenantId_Type_ShiftId_CreatedAt",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_CancelLogs_ShiftId",
                table: "CancelLogs");

            migrationBuilder.DropIndex(
                name: "IX_CancelLogs_TenantId_ShiftId_CancelledAt",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "ItemName",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "CancelLogs");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "CancelLogs");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2647), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2647) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2641), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2643) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2649), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2649) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2651), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2651) });
        }
    }
}
