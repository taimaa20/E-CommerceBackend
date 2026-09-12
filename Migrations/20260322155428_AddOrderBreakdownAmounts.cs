using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderBreakdownAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("255bb560-a4d8-4d9e-957c-58ca20c84276"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("46967d7d-942d-4ba0-8e88-e0ab245b731d"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("845279e0-f910-4c69-9c0c-b8aeb17b838a"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("cfdbb877-5ddc-4f7f-b0ce-5617d41b7b9e"));

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceChargeAmount",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("19410b8e-d2f1-4fa7-b47e-23abc1c1b7c3"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("73706de2-da65-42a4-888d-b184ab874c50"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("da292b39-8260-4043-b944-e85a3296b219"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("fde69a1f-ce9d-4407-87e2-397264cf2698"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("19410b8e-d2f1-4fa7-b47e-23abc1c1b7c3"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("73706de2-da65-42a4-888d-b184ab874c50"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("da292b39-8260-4043-b944-e85a3296b219"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fde69a1f-ce9d-4407-87e2-397264cf2698"));

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ServiceChargeAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "Orders");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("255bb560-a4d8-4d9e-957c-58ca20c84276"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("46967d7d-942d-4ba0-8e88-e0ab245b731d"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("845279e0-f910-4c69-9c0c-b8aeb17b838a"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("cfdbb877-5ddc-4f7f-b0ce-5617d41b7b9e"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" }
                });
        }
    }
}
