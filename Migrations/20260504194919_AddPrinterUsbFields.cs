using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterUsbFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsbPortName",
                table: "Printers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WindowsPrinterName",
                table: "Printers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6391), new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6392) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6385), new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6388) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6394), new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6395) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6393), new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6393) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6396), new DateTime(2026, 5, 4, 19, 49, 14, 790, DateTimeKind.Utc).AddTicks(6396) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsbPortName",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "WindowsPrinterName",
                table: "Printers");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1685), new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1685) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1678), new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1680) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1688), new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1688) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1686), new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1687) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1693), new DateTime(2026, 5, 4, 18, 15, 11, 453, DateTimeKind.Utc).AddTicks(1694) });
        }
    }
}
