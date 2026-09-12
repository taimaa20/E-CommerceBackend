using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierReceiptPrinter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReceiptPrinterId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Printers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "ReceiptPrinterId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1685), null, new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1685) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "ReceiptPrinterId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1678), null, new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1680) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "ReceiptPrinterId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1688), null, new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1688) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "ReceiptPrinterId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1686), null, new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1687) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "ReceiptPrinterId", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1693), null, new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1694) });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReceiptPrinterId",
                table: "Users",
                column: "ReceiptPrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Tenant_ReceiptPrinter",
                table: "Users",
                columns: new[] { "TenantId", "ReceiptPrinterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Printers_Tenant_Default",
                table: "Printers",
                columns: new[] { "TenantId", "IsDefault", "IsActive" },
                filter: "\"IsDefault\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Printers_ReceiptPrinterId",
                table: "Users",
                column: "ReceiptPrinterId",
                principalTable: "Printers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Printers_ReceiptPrinterId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReceiptPrinterId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Tenant_ReceiptPrinter",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Printers_Tenant_Default",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "ReceiptPrinterId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Printers");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5882), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5882) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5874), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5877) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5886), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5886) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5884), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5885) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5888), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5888) });
        }
    }
}
