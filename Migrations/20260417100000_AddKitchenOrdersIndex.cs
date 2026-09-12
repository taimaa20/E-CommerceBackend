using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenOrdersIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Composite index optimised for the kitchen "today" query:
            // WHERE TenantId = X
            //   AND (CreatedAt >= @today
            //     OR (CreatedAt < @today AND Status NOT IN (2,3,5,6)))
            // Leading column TenantId narrows the scan to a single tenant;
            // Status + CreatedAt together cover both branches of the OR predicate.
            migrationBuilder.CreateIndex(
                name: "IX_Orders_Kitchen",
                table: "Orders",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Kitchen",
                table: "Orders");
        }
    }
}
