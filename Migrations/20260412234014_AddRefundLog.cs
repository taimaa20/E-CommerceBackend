using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FacebookUrl, GoogleMapsLocationUrl, GoogleReviewUrl, InstagramUrl, TikTokUrl
            // were already applied directly to the DB — skip AddColumn to avoid "column already exists".

            migrationBuilder.CreateTable(
                name: "RefundLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RefundReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RefundNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginalPaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProcessedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProcessedByRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundLogs_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefundLogs_OrderId",
                table: "RefundLogs",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefundLogs");

            // Social URL columns intentionally not dropped — they pre-existed this migration.
        }
    }
}
