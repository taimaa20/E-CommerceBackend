using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTalabatOrderSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderSource",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TalabatCustomerName",
                table: "Orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TalabatCustomerPhone",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TalabatDeliveryFee",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TalabatOrderNumber",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TalabatPaymentMethod",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TalabatServiceFee",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_TalabatOrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "TalabatOrderNumber" },
                filter: "\"TalabatOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_TalabatOrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderSource",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TalabatCustomerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TalabatCustomerPhone",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TalabatDeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TalabatOrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TalabatPaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TalabatServiceFee",
                table: "Orders");
        }
    }
}
