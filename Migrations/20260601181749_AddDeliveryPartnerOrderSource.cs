using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPartnerOrderSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryPartnerCode",
                table: "Orders",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryPartnerId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPartnerName",
                table: "Orders",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPartnerNameAr",
                table: "Orders",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerCustomerName",
                table: "Orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerCustomerPhone",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerDeliveryFee",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerOrderNumber",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerPaymentMethod",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PartnerPickupTime",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerServiceFee",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LineTotalSnapshot",
                table: "OrderItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerPriceSnapshot",
                table: "OrderItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceSnapshot",
                table: "OrderItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryPartnerId",
                table: "Orders",
                column: "DeliveryPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_Partner_CreatedAt",
                table: "Orders",
                columns: new[] { "TenantId", "DeliveryPartnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_Partner_OrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "DeliveryPartnerId", "PartnerOrderNumber" },
                unique: true,
                filter: "\"DeliveryPartnerId\" IS NOT NULL AND \"PartnerOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_Source_CreatedAt",
                table: "Orders",
                columns: new[] { "TenantId", "OrderSource", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DeliveryPartners_DeliveryPartnerId",
                table: "Orders",
                column: "DeliveryPartnerId",
                principalTable: "DeliveryPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DeliveryPartners_DeliveryPartnerId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DeliveryPartnerId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_Partner_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_Partner_OrderNumber",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_Source_CreatedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPartnerCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPartnerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPartnerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPartnerNameAr",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PartnerCustomerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PartnerCustomerPhone",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PartnerDeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PartnerOrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PartnerPaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PartnerPickupTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PartnerServiceFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LineTotalSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PartnerPriceSnapshot",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "UnitPriceSnapshot",
                table: "OrderItems");
        }
    }
}
