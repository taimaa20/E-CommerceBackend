using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryZoneGeoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CenterLatitude",
                table: "DeliveryZones",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CenterLongitude",
                table: "DeliveryZones",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RadiusMeters",
                table: "DeliveryZones",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_TenantId_IsActive_CenterLatitude_CenterLongit~",
                table: "DeliveryZones",
                columns: new[] { "TenantId", "IsActive", "CenterLatitude", "CenterLongitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryZones_TenantId_IsActive_CenterLatitude_CenterLongit~",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "CenterLatitude",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "CenterLongitude",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "RadiusMeters",
                table: "DeliveryZones");
        }
    }
}
