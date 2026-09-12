using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPartnerPriceAdjustmentAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryPartnerCode",
                table: "PartnerPriceOverrideAudits",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryPartnerId",
                table: "PartnerPriceOverrideAudits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPartnerName",
                table: "PartnerPriceOverrideAudits",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPartnerNameAr",
                table: "PartnerPriceOverrideAudits",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NewDiscountAmount",
                table: "PartnerPriceOverrideAudits",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "PartnerPriceOverrideAudits",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OldDiscountAmount",
                table: "PartnerPriceOverrideAudits",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "PartnerPriceOverrideAudits",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryPartnerCode",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "DeliveryPartnerId",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "DeliveryPartnerName",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "DeliveryPartnerNameAr",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "NewDiscountAmount",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "OldDiscountAmount",
                table: "PartnerPriceOverrideAudits");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "PartnerPriceOverrideAudits");
        }
    }
}
