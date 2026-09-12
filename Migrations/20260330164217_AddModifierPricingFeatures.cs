using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddModifierPricingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("0ee4a48c-4e37-4eac-bb42-a5ef8be82a51"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("95e56f62-35b6-4999-93bd-6dd145d3410b"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("9e383beb-8624-4bdf-b9e5-ae7a41da431a"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fdc054ad-230e-44f0-a6ae-e3f4d29de065"));

            migrationBuilder.AddColumn<int>(
                name: "FreeQuantityLimit",
                table: "Modifiers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFree",
                table: "Modifiers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LinkedMaterialAmount",
                table: "Modifiers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedRawMaterialId",
                table: "Modifiers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxQuantity",
                table: "Modifiers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PricingType",
                table: "Modifiers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AvailableForOrderTypes",
                table: "ModifierGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DisplayType",
                table: "ModifierGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "ModifierGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PrintInKitchen",
                table: "ModifierGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PrintOnReceipt",
                table: "ModifierGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("1e1607a8-179f-4fd8-8e4f-5fd4975a7d56"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("40980373-eaa9-4d76-9fca-be4d667a9c24"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("4796edec-3b60-4470-b174-3a8d1f30bc0e"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("adc2a225-a469-40fd-9c92-9a84e65b93cb"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("1e1607a8-179f-4fd8-8e4f-5fd4975a7d56"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("40980373-eaa9-4d76-9fca-be4d667a9c24"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("4796edec-3b60-4470-b174-3a8d1f30bc0e"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("adc2a225-a469-40fd-9c92-9a84e65b93cb"));

            migrationBuilder.DropColumn(
                name: "FreeQuantityLimit",
                table: "Modifiers");

            migrationBuilder.DropColumn(
                name: "IsFree",
                table: "Modifiers");

            migrationBuilder.DropColumn(
                name: "LinkedMaterialAmount",
                table: "Modifiers");

            migrationBuilder.DropColumn(
                name: "LinkedRawMaterialId",
                table: "Modifiers");

            migrationBuilder.DropColumn(
                name: "MaxQuantity",
                table: "Modifiers");

            migrationBuilder.DropColumn(
                name: "PricingType",
                table: "Modifiers");

            migrationBuilder.DropColumn(
                name: "AvailableForOrderTypes",
                table: "ModifierGroups");

            migrationBuilder.DropColumn(
                name: "DisplayType",
                table: "ModifierGroups");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "ModifierGroups");

            migrationBuilder.DropColumn(
                name: "PrintInKitchen",
                table: "ModifierGroups");

            migrationBuilder.DropColumn(
                name: "PrintOnReceipt",
                table: "ModifierGroups");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("0ee4a48c-4e37-4eac-bb42-a5ef8be82a51"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("95e56f62-35b6-4999-93bd-6dd145d3410b"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("9e383beb-8624-4bdf-b9e5-ae7a41da431a"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("fdc054ad-230e-44f0-a6ae-e3f4d29de065"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" }
                });
        }
    }
}
