using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWasteLogCancelLogExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Extend StockBatches with expiry-tracking columns ─────────────────
            migrationBuilder.AddColumn<bool>(
                name: "NearExpiryNotified",
                table: "StockBatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExpiredWasteLogged",
                table: "StockBatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // ── Index on StockBatch.ExpiryDate for expiry-job queries ────────────
            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_ExpiryDate",
                table: "StockBatches",
                column: "ExpiryDate");

            // ── CancelReasons lookup table ────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "CancelReasons",
                columns: table => new
                {
                    Id         = table.Column<int>(type: "integer", nullable: false)
                                     .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code       = table.Column<string>(type: "character varying(50)",  maxLength: 50,  nullable: false),
                    Name       = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameAr     = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequiresNote = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive   = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder  = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TenantId   = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_CancelReasons", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_CancelReasons_Code",
                table: "CancelReasons",
                column: "Code");

            // Default seed data (global — TenantId = null)
            migrationBuilder.InsertData(
                table: "CancelReasons",
                columns: new[] { "Id", "Code", "Name", "NameAr", "RequiresNote", "IsActive", "SortOrder" },
                values: new object[,]
                {
                    { 1, "CLIENT_CHANGED_MIND", "Client changed mind",   "العميل غير رأيه",   false, true, 1 },
                    { 2, "WRONG_ORDER_ENTERED", "Wrong order entered",   "تم إدخال طلب خاطئ", false, true, 2 },
                    { 3, "CLIENT_LEFT",         "Client left",           "العميل غادر",        false, true, 3 },
                    { 4, "ALLERGY_CONCERN",     "Allergy concern",       "مشكلة حساسية",       false, true, 4 },
                    { 5, "ITEM_UNAVAILABLE",    "Item unavailable",      "المنتج غير متوفر",   false, true, 5 },
                    { 6, "OTHER",               "Other",                 "سبب آخر",            true,  true, 6 },
                });

            // ── WasteLogs table ───────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "WasteLogs",
                columns: table => new
                {
                    Id               = table.Column<Guid>(type: "uuid", nullable: false),
                    Category         = table.Column<int>(type: "integer", nullable: false),
                    ItemId           = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName         = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity         = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    Unit             = table.Column<string>(type: "character varying(30)",  maxLength: 30,  nullable: false),
                    Reason           = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LoggedById       = table.Column<Guid>(type: "uuid", nullable: true),
                    LoggedByName     = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SourceOrderId    = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceOrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId         = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WasteLogs_Users_LoggedById",
                        column: x => x.LoggedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WasteLogs_Orders_SourceOrderId",
                        column: x => x.SourceOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WasteLogs_OrderItems_SourceOrderItemId",
                        column: x => x.SourceOrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Indexes for WasteLogs (Rule 2)
            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_CreatedAt",
                table: "WasteLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_TenantId_Category_CreatedAt",
                table: "WasteLogs",
                columns: new[] { "TenantId", "Category", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_LoggedById",
                table: "WasteLogs",
                column: "LoggedById");

            // ── CancelLogs table ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "CancelLogs",
                columns: table => new
                {
                    Id                = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId           = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId       = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledById     = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledByName   = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CancelledByRole   = table.Column<int>(type: "integer", nullable: false),
                    OrderTime         = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt       = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    CancelReasonCode  = table.Column<string>(type: "character varying(50)",  maxLength: 50,  nullable: false),
                    CancelReasonNote  = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WasteLogId        = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId          = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CancelLogs_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CancelLogs_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CancelLogs_Users_CancelledById",
                        column: x => x.CancelledById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CancelLogs_WasteLogs_WasteLogId",
                        column: x => x.WasteLogId,
                        principalTable: "WasteLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Indexes for CancelLogs (Rule 2)
            migrationBuilder.CreateIndex(
                name: "IX_CancelLogs_TenantId_CancelledAt",
                table: "CancelLogs",
                columns: new[] { "TenantId", "CancelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CancelLogs_CancelledById_CancelledAt",
                table: "CancelLogs",
                columns: new[] { "CancelledById", "CancelledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CancelLogs");
            migrationBuilder.DropTable(name: "WasteLogs");
            migrationBuilder.DropTable(name: "CancelReasons");

            migrationBuilder.DropIndex(name: "IX_StockBatches_ExpiryDate", table: "StockBatches");

            migrationBuilder.DropColumn(name: "NearExpiryNotified", table: "StockBatches");
            migrationBuilder.DropColumn(name: "ExpiredWasteLogged",  table: "StockBatches");
        }
    }
}
