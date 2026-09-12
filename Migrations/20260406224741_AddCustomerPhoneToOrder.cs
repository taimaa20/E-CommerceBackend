using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPhoneToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("12575421-e8a9-47ee-8df5-ce6428ead050"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13fa12fd-84c5-4903-852b-360eab237787"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("4cbb573b-82f2-4f08-9db1-5c2a931cd0da"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b412a1d6-ca42-4fba-be58-18af05e6169e"));

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("0b77e98f-0665-4268-8d0e-6cbdc3890e7f"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("3deb02e9-310b-4c10-b72a-bbf9ba89ddfb"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("551c6c00-2368-4a46-ad29-ec3ed2b90d76"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("b7f30289-c49b-482c-9a40-cd10854f90e8"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("0b77e98f-0665-4268-8d0e-6cbdc3890e7f"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("3deb02e9-310b-4c10-b72a-bbf9ba89ddfb"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("551c6c00-2368-4a46-ad29-ec3ed2b90d76"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b7f30289-c49b-482c-9a40-cd10854f90e8"));

            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "Orders");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("12575421-e8a9-47ee-8df5-ce6428ead050"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("13fa12fd-84c5-4903-852b-360eab237787"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("4cbb573b-82f2-4f08-9db1-5c2a931cd0da"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("b412a1d6-ca42-4fba-be58-18af05e6169e"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" }
                });
        }
    }
}
