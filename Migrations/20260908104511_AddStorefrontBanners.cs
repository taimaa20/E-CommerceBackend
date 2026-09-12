using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontBanners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorefrontBanners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TitleAr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Subtitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    SubtitleAr = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MobileImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MobileImageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CtaLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CtaLabelAr = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LinkType = table.Column<int>(type: "integer", nullable: false),
                    LinkValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Placement = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontBanners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontBanners_Tenant_Placement_Active_Sort",
                table: "StorefrontBanners",
                columns: new[] { "TenantId", "Placement", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorefrontBanners");
        }
    }
}
