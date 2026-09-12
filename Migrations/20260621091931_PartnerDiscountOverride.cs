using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class PartnerDiscountOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasPartnerDiscountOverride",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PartnerDiscountReason",
                table: "OrderItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PartnerDiscountUpdatedAt",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerDiscountUpdatedBy",
                table: "OrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerDiscountedUnitPrice",
                table: "OrderItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerOriginalUnitPrice",
                table: "OrderItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartnerPriceOverrideAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    NewPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerPriceOverrideAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_PartnerDiscountUpdatedBy",
                table: "OrderItems",
                column: "PartnerDiscountUpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_Tenant_PartnerDiscountOverride",
                table: "OrderItems",
                columns: new[] { "TenantId", "HasPartnerDiscountOverride" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerPriceOverrideAudit_Tenant_Item_Time",
                table: "PartnerPriceOverrideAudits",
                columns: new[] { "TenantId", "OrderItemId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerPriceOverrideAudit_Tenant_Order_Time",
                table: "PartnerPriceOverrideAudits",
                columns: new[] { "TenantId", "OrderId", "UpdatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Users_PartnerDiscountUpdatedBy",
                table: "OrderItems",
                column: "PartnerDiscountUpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Users_PartnerDiscountUpdatedBy",
                table: "OrderItems");

            migrationBuilder.DropTable(
                name: "PartnerPriceOverrideAudits");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_PartnerDiscountUpdatedBy",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_Tenant_PartnerDiscountOverride",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "HasPartnerDiscountOverride",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PartnerDiscountReason",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PartnerDiscountUpdatedAt",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PartnerDiscountUpdatedBy",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PartnerDiscountedUnitPrice",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "PartnerOriginalUnitPrice",
                table: "OrderItems");
        }
    }
}
