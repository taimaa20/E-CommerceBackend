using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRawMaterialNameToPurchaseOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("3ed027c5-9038-4ce4-be7c-713a48a1bca5"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("8d8e9a32-0e15-4398-a50b-5c49d4ec4d81"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("9c803a54-a2c4-4247-95fa-014ed0f80717"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d97e40a9-6519-4b7a-9b57-a7310882cb65"));

            migrationBuilder.AddColumn<string>(
                name: "RawMaterialName",
                table: "PurchaseOrderItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("01b345d2-8fdc-4986-9377-87fe82fca2ae"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("6172af54-dec2-49aa-a1cc-b65e50b9702c"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("69c43f54-e9eb-4e4c-ae45-6eb93976ff58"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("fa43dd83-4637-41ea-855c-7b0db284af12"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("01b345d2-8fdc-4986-9377-87fe82fca2ae"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("6172af54-dec2-49aa-a1cc-b65e50b9702c"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("69c43f54-e9eb-4e4c-ae45-6eb93976ff58"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("fa43dd83-4637-41ea-855c-7b0db284af12"));

            migrationBuilder.DropColumn(
                name: "RawMaterialName",
                table: "PurchaseOrderItems");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("3ed027c5-9038-4ce4-be7c-713a48a1bca5"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("8d8e9a32-0e15-4398-a50b-5c49d4ec4d81"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("9c803a54-a2c4-4247-95fa-014ed0f80717"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("d97e40a9-6519-4b7a-9b57-a7310882cb65"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" }
                });
        }
    }
}
