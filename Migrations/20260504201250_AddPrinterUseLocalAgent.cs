using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterUseLocalAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseLocalAgent",
                table: "Printers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(438), new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(438) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(430), new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(433) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(442), new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(442) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(440), new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(440) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(443), new DateTime(2026, 5, 4, 20, 12, 49, 297, DateTimeKind.Utc).AddTicks(444) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseLocalAgent",
                table: "Printers");

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
    }
}
