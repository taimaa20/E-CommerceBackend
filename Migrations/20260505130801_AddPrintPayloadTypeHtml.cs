using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintPayloadTypeHtml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayloadType",
                table: "PrintJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(5001), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(5001) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(4994), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(4997) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(5004), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(5004) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(5002), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(5002) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(5005), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc).AddTicks(5005) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayloadType",
                table: "PrintJobs");

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
    }
}
