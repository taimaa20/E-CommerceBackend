using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPartnersManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryPartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DescriptionAr = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DefaultPricingRuleType = table.Column<int>(type: "integer", nullable: false),
                    DefaultPricingRuleValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IntegrationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IntegrationSettingsJson = table.Column<string>(type: "text", nullable: true),
                    CredentialsJson = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryPartnerActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryPartnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    PerformedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPartnerActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryPartnerActivityLogs_DeliveryPartners_DeliveryPartne~",
                        column: x => x.DeliveryPartnerId,
                        principalTable: "DeliveryPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeliveryPartnerActivityLogs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryPartnerProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryPartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerProductId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    PricingRuleType = table.Column<int>(type: "integer", nullable: true),
                    PricingRuleValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CustomPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AvailableStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AvailableEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AvailableFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    AvailableTo = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPartnerProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryPartnerProducts_DeliveryPartners_DeliveryPartnerId",
                        column: x => x.DeliveryPartnerId,
                        principalTable: "DeliveryPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryPartnerProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerActivity_Tenant_Partner_Time",
                table: "DeliveryPartnerActivityLogs",
                columns: new[] { "TenantId", "DeliveryPartnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerActivity_Tenant_Product_Time",
                table: "DeliveryPartnerActivityLogs",
                columns: new[] { "TenantId", "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerActivityLogs_DeliveryPartnerId",
                table: "DeliveryPartnerActivityLogs",
                column: "DeliveryPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerActivityLogs_ProductId",
                table: "DeliveryPartnerActivityLogs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerProducts_DeliveryPartnerId",
                table: "DeliveryPartnerProducts",
                column: "DeliveryPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerProducts_ProductId",
                table: "DeliveryPartnerProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerProducts_Tenant_Partner_ExternalId",
                table: "DeliveryPartnerProducts",
                columns: new[] { "TenantId", "DeliveryPartnerId", "PartnerProductId" },
                unique: true,
                filter: "\"PartnerProductId\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerProducts_Tenant_Partner_Product",
                table: "DeliveryPartnerProducts",
                columns: new[] { "TenantId", "DeliveryPartnerId", "ProductId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartnerProducts_Tenant_Product_Enabled",
                table: "DeliveryPartnerProducts",
                columns: new[] { "TenantId", "ProductId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartners_Tenant_Code",
                table: "DeliveryPartners",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPartners_Tenant_Status_Sort",
                table: "DeliveryPartners",
                columns: new[] { "TenantId", "Status", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryPartnerActivityLogs");

            migrationBuilder.DropTable(
                name: "DeliveryPartnerProducts");

            migrationBuilder.DropTable(
                name: "DeliveryPartners");
        }
    }
}
