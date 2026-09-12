using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddModifierDefaultAndActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("04dd9eb6-b690-4c12-9057-cdcde76b8110"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("069376c9-b95c-4015-8a65-fb5474ada5f8"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("41a0d61c-da86-4e1f-b338-23a7d0a76675"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d8c3768e-0bc3-43ad-8d67-0c1a87ed0fd6"));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Modifiers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Modifiers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MenuAccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientIpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AccessType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferrerUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QrCodeAccess",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    QrCodeToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccessCount = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrCodeAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QrCodeAccess_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantMenuSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MenuTitleAr = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MenuDescription = table.Column<string>(type: "text", nullable: true),
                    MenuDescriptionAr = table.Column<string>(type: "text", nullable: true),
                    HeroBannerUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HeroBannerTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HeroBannerTitleAr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DefaultProductImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EnableQrAccess = table.Column<bool>(type: "boolean", nullable: false),
                    QrCodeDisplayText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QrCodeDisplayTextAr = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ShowPrices = table.Column<bool>(type: "boolean", nullable: false),
                    ShowAllergenInfo = table.Column<bool>(type: "boolean", nullable: false),
                    ShowIngredientsBreakdown = table.Column<bool>(type: "boolean", nullable: false),
                    RequireLanguageSelection = table.Column<bool>(type: "boolean", nullable: false),
                    AllowArabic = table.Column<bool>(type: "boolean", nullable: false),
                    AllowEnglish = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCategoryFilter = table.Column<bool>(type: "boolean", nullable: false),
                    EnableSearch = table.Column<bool>(type: "boolean", nullable: false),
                    EnableSort = table.Column<bool>(type: "boolean", nullable: false),
                    ThemePrimaryColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMenuSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantMenuSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("73d37ea2-2e53-43c3-8249-6be6bb548911"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("8f2e9d05-8493-4ab7-a27c-0dc1ce6355a8"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("bad35acb-3645-4c9b-9022-4ce1b772b239"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("c17f6f54-9d55-4add-9139-43dc01b1af5d"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_QrCodeAccess_TenantId",
                table: "QrCodeAccess",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMenuSettings_TenantId",
                table: "TenantMenuSettings",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuAccessLogs");

            migrationBuilder.DropTable(
                name: "QrCodeAccess");

            migrationBuilder.DropTable(
                name: "TenantMenuSettings");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("73d37ea2-2e53-43c3-8249-6be6bb548911"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("8f2e9d05-8493-4ab7-a27c-0dc1ce6355a8"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bad35acb-3645-4c9b-9022-4ce1b772b239"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("c17f6f54-9d55-4add-9139-43dc01b1af5d"));

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Modifiers");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Modifiers");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("04dd9eb6-b690-4c12-9057-cdcde76b8110"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("069376c9-b95c-4015-8a65-fb5474ada5f8"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("41a0d61c-da86-4e1f-b338-23a7d0a76675"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("d8c3768e-0bc3-43ad-8d67-0c1a87ed0fd6"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" }
                });
        }
    }
}
