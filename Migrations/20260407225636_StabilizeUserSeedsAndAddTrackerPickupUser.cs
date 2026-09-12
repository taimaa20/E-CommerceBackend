using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class StabilizeUserSeedsAndAddTrackerPickupUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[] { new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"), 0m, "Tracker Pickup", 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 4, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "trackerpickup" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"));
        }
    }
}
