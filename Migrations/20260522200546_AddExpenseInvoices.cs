using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SupplierPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SupplierTaxNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethodDetail = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseInvoices_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseInvoiceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FullUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseInvoiceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseInvoiceAttachments_ExpenseInvoices_ExpenseInvoiceId",
                        column: x => x.ExpenseInvoiceId,
                        principalTable: "ExpenseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExpenseInvoiceAttachments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoiceAttachments_ExpenseInvoiceId",
                table: "ExpenseInvoiceAttachments",
                column: "ExpenseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoiceAttachments_Tenant_Invoice",
                table: "ExpenseInvoiceAttachments",
                columns: new[] { "TenantId", "ExpenseInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoiceAttachments_UploadedById",
                table: "ExpenseInvoiceAttachments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_CreatedById",
                table: "ExpenseInvoices",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Tenant_Category",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "ExpenseCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Tenant_Date",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Tenant_InvoiceNumber",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Tenant_Status_Date",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseInvoices_Tenant_Supplier",
                table: "ExpenseInvoices",
                columns: new[] { "TenantId", "SupplierId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseInvoiceAttachments");

            migrationBuilder.DropTable(
                name: "ExpenseInvoices");
        }
    }
}
