using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceExpenseInvoiceDuplicateDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_Tenant_InvoiceNumber",
                table: "ExpenseInvoices");

            migrationBuilder.AddColumn<int>(
                name: "DuplicateInvoiceBehavior",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ExpenseInvoiceAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseInvoiceAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseInvoiceAuditLogs_ExpenseInvoices_ExpenseInvoiceId",
                        column: x => x.ExpenseInvoiceId,
                        principalTable: "ExpenseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Duplicate_ManualSupplier",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "SupplierName", "InvoiceNumber", "InvoiceDate" },
                filter: "\"SupplierId\" IS NULL AND \"SupplierName\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Duplicate_RegisteredSupplier",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "SupplierId", "InvoiceNumber", "InvoiceDate" },
                filter: "\"SupplierId\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Tenant_InvoiceNumber",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "InvoiceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoiceAuditLogs_ExpenseInvoiceId",
                table: "ExpenseInvoiceAuditLogs",
                column: "ExpenseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoiceAuditLogs_Tenant_Invoice_Date",
                table: "ExpenseInvoiceAuditLogs",
                columns: new[] { "TenantId", "ExpenseInvoiceId", "AcknowledgedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseInvoiceAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_Duplicate_ManualSupplier",
                table: "ExpenseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_Duplicate_RegisteredSupplier",
                table: "ExpenseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseInvoices_Tenant_InvoiceNumber",
                table: "ExpenseInvoices");

            migrationBuilder.DropColumn(
                name: "DuplicateInvoiceBehavior",
                table: "SystemSettings");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Tenant_InvoiceNumber",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }
    }
}
