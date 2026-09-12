using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeparateDisplayAndSystemOrderNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_DisplayOrderNumber",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_DisplayOrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "DisplayOrderNumber" },
                filter: "\"DisplayOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Orders_Tenant_OrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_DisplayOrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "UX_Orders_Tenant_OrderNumber",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_DisplayOrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "DisplayOrderNumber" },
                unique: true,
                filter: "\"DisplayOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL");
        }
    }
}
