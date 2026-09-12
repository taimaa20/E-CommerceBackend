using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoucherApplied",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoucherAppliedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherDiscountAmount",
                table: "Orders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "VoucherUsageAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherUsageAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherUsageAudits_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoucherUsageAudits_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_Voucher",
                table: "Orders",
                columns: new[] { "TenantId", "IsVoucherApplied", "VoucherAppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsageAudits_CreatedByUserId",
                table: "VoucherUsageAudits",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsageAudits_OrderId",
                table: "VoucherUsageAudits",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsageAudits_Tenant_BusinessDate",
                table: "VoucherUsageAudits",
                columns: new[] { "TenantId", "BusinessDate", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsageAudits_Tenant_Order",
                table: "VoucherUsageAudits",
                columns: new[] { "TenantId", "OrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoucherUsageAudits");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_Voucher",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsVoucherApplied",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherAppliedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VoucherDiscountAmount",
                table: "Orders");
        }
    }
}
