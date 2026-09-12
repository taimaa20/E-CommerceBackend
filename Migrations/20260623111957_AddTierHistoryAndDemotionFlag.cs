using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTierHistoryAndDemotionFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TierDemotionEnabled",
                table: "MarketingSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CustomerTierHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousTierId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewTierId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousLegacyTier = table.Column<int>(type: "integer", nullable: false),
                    NewLegacyTier = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    LifetimePointsAtChange = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LifetimeSpendAtChange = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTierHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerTierHistories_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerTierHistories_LoyaltyTiers_NewTierId",
                        column: x => x.NewTierId,
                        principalTable: "LoyaltyTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTierHistories_CustomerId",
                table: "CustomerTierHistories",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTierHistories_NewTierId",
                table: "CustomerTierHistories",
                column: "NewTierId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTierHistories_TenantId_ChangedAt",
                table: "CustomerTierHistories",
                columns: new[] { "TenantId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTierHistories_TenantId_CustomerId_ChangedAt",
                table: "CustomerTierHistories",
                columns: new[] { "TenantId", "CustomerId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerTierHistories");

            migrationBuilder.DropColumn(
                name: "TierDemotionEnabled",
                table: "MarketingSettings");
        }
    }
}
