using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyDashboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_TenantId_LifetimePoints",
                table: "CustomerWallets",
                columns: new[] { "TenantId", "LifetimePoints" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_TenantId_RedeemedPoints",
                table: "CustomerWallets",
                columns: new[] { "TenantId", "RedeemedPoints" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWallets_TenantId_TotalOrders",
                table: "CustomerWallets",
                columns: new[] { "TenantId", "TotalOrders" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerWallets_TenantId_LifetimePoints",
                table: "CustomerWallets");

            migrationBuilder.DropIndex(
                name: "IX_CustomerWallets_TenantId_RedeemedPoints",
                table: "CustomerWallets");

            migrationBuilder.DropIndex(
                name: "IX_CustomerWallets_TenantId_TotalOrders",
                table: "CustomerWallets");
        }
    }
}
