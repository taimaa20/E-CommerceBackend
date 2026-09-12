using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenRoutingAndPrintQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "KitchenId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Kitchens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kitchens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    KitchenId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrinterId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayloadBase64 = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OrderNumberSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    KitchenNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Printers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    KitchenId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    CodePage = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsReceiptPrinter = table.Column<bool>(type: "boolean", nullable: false),
                    CopiesPerJob = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Printers_Kitchens_KitchenId",
                        column: x => x.KitchenId,
                        principalTable: "Kitchens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5882), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5882) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5874), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5877) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5886), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5886) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5884), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5885) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5888), new DateTime(2026, 5, 3, 16, 39, 2, 608, DateTimeKind.Utc).AddTicks(5888) });

            migrationBuilder.CreateIndex(
                name: "IX_Products_KitchenId",
                table: "Products",
                column: "KitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Tenant_Kitchen",
                table: "Products",
                columns: new[] { "TenantId", "KitchenId" });

            migrationBuilder.CreateIndex(
                name: "IX_Printers_KitchenId",
                table: "Printers",
                column: "KitchenId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_Tenant_Kitchen_Active",
                table: "Printers",
                columns: new[] { "TenantId", "KitchenId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Printers_Tenant_Receipt_Active",
                table: "Printers",
                columns: new[] { "TenantId", "IsReceiptPrinter", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Tenant_Idempotency",
                table: "PrintJobs",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Tenant_Order",
                table: "PrintJobs",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_Tenant_Status_Next",
                table: "PrintJobs",
                columns: new[] { "TenantId", "Status", "NextAttemptAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Kitchens_KitchenId",
                table: "Products",
                column: "KitchenId",
                principalTable: "Kitchens",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Kitchens_KitchenId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Printers");

            migrationBuilder.DropTable(
                name: "PrintJobs");

            migrationBuilder.DropTable(
                name: "Kitchens");

            migrationBuilder.DropIndex(
                name: "IX_Products_KitchenId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Tenant_Kitchen",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "KitchenId",
                table: "Products");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3736), new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3736) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3727), new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3731) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3739), new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3740) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3737), new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3738) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3741), new DateTime(2026, 5, 3, 14, 31, 48, 461, DateTimeKind.Utc).AddTicks(3741) });
        }
    }
}
