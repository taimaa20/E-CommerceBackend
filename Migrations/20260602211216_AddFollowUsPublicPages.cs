using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUsPublicPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "SystemSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "SystemSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestaurantTagline",
                table: "SystemSettings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "SystemSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "SystemSettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FollowUsClicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Referrer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowUsClicks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUsClicks_Tenant_ClickedAt",
                table: "FollowUsClicks",
                columns: new[] { "TenantId", "ClickedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUsClicks_Tenant_Platform_ClickedAt",
                table: "FollowUsClicks",
                columns: new[] { "TenantId", "Platform", "ClickedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FollowUsClicks");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "RestaurantTagline",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "SystemSettings");
        }
    }
}
