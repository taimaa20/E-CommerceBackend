using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTotalPriceDifferenceMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CorrectTotal",
                table: "PartnerPriceOverrideAudits",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectionMode",
                table: "PartnerPriceOverrideAudits",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DifferenceAmount",
                table: "PartnerPriceOverrideAudits",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalTotal",
                table: "PartnerPriceOverrideAudits",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CorrectPartnerTotal",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPartnerTotal",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceDifferenceCorrectionMode",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDifferenceAmount",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectTotal",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "CorrectionMode",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "DifferenceAmount",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "OriginalTotal",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "CorrectPartnerTotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OriginalPartnerTotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PriceDifferenceCorrectionMode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TotalDifferenceAmount",
                table: "Orders");
        }
    }
}
