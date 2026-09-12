using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerMobileFinalFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowMobileCancelPreparing",
                table: "SystemSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyBronzeThreshold",
                table: "SystemSettings",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyGoldThreshold",
                table: "SystemSettings",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyMaximumRedeemPoints",
                table: "SystemSettings",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyMinimumRedeemPoints",
                table: "SystemSettings",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyPointValue",
                table: "SystemSettings",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltySilverThreshold",
                table: "SystemSettings",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyVipThreshold",
                table: "SystemSettings",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyContent",
                table: "SystemSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyContentAr",
                table: "SystemSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyTitle",
                table: "SystemSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Privacy Policy");

            migrationBuilder.AddColumn<string>(
                name: "PrivacyTitleAr",
                table: "SystemSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "سياسة الخصوصية");

            migrationBuilder.AddColumn<string>(
                name: "RestaurantAddressAr",
                table: "SystemSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapchatUrl",
                table: "SystemSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsContent",
                table: "SystemSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsContentAr",
                table: "SystemSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsTitle",
                table: "SystemSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Terms & Conditions");

            migrationBuilder.AddColumn<string>(
                name: "TermsTitleAr",
                table: "SystemSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "الشروط والأحكام");

            migrationBuilder.AddColumn<string>(
                name: "WorkingHours",
                table: "SystemSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XUrl",
                table: "SystemSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YouTubeUrl",
                table: "SystemSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageAr",
                table: "Notifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleAr",
                table: "Notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CustomerDevices",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "CustomerMobileAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerMobileAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerMobileAuditLogs_CustomerAccounts_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Tenant_Customer_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "TenantId", "CustomerId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMobileAuditLogs_CustomerId",
                table: "CustomerMobileAuditLogs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMobileAuditLogs_Tenant_Customer_ActionAt",
                table: "CustomerMobileAuditLogs",
                columns: new[] { "TenantId", "CustomerId", "ActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMobileAuditLogs_Tenant_Notification",
                table: "CustomerMobileAuditLogs",
                columns: new[] { "TenantId", "NotificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerMobileAuditLogs_Tenant_Order",
                table: "CustomerMobileAuditLogs",
                columns: new[] { "TenantId", "OrderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerMobileAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_Tenant_Customer_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AllowMobileCancelPreparing",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyBronzeThreshold",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyGoldThreshold",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyMaximumRedeemPoints",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyMinimumRedeemPoints",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointValue",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltySilverThreshold",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyVipThreshold",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "PrivacyContent",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "PrivacyContentAr",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "PrivacyTitle",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "PrivacyTitleAr",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "RestaurantAddressAr",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "SnapchatUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "TermsContent",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "TermsContentAr",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "TermsTitle",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "TermsTitleAr",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WorkingHours",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "XUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "YouTubeUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "MessageAr",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TitleAr",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CustomerDevices");
        }
    }
}
