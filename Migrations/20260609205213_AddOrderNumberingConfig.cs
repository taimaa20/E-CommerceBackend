using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberingConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_OrderDisplaySequences_Tenant_Code_Year_Month",
                table: "OrderDisplaySequences");

            migrationBuilder.AlterColumn<string>(
                name: "OrderTypeCode",
                table: "OrderDisplaySequences",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4)",
                oldMaxLength: 4);

            migrationBuilder.AddColumn<string>(
                name: "BucketKey",
                table: "OrderDisplaySequences",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows to the legacy monthly bucket so the new unique
            // index (TenantId, OrderTypeCode, BucketKey) does not collide on the
            // empty-string default, AND so the Postgres ON CONFLICT in
            // OrderDisplayNumberService keeps hitting the same row — current
            // per-channel counters continue uninterrupted after deploy.
            migrationBuilder.Sql(@"
UPDATE ""OrderDisplaySequences""
SET ""BucketKey"" = 'MONTH:' || LPAD(""Year""::text, 4, '0') || LPAD(""Month""::text, 2, '0')
WHERE ""BucketKey"" = '' OR ""BucketKey"" IS NULL;");

            migrationBuilder.CreateTable(
                name: "OrderNumberingConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResetStrategy = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    IncludeDate = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeMonth = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeYear = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeShiftNumber = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeBranchCode = table.Column<bool>(type: "boolean", nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderNumberingConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_OrderDisplaySequences_Tenant_Code_Bucket",
                table: "OrderDisplaySequences",
                columns: new[] { "TenantId", "OrderTypeCode", "BucketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OrderNumberingConfigs_Tenant",
                table: "OrderNumberingConfigs",
                column: "TenantId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderNumberingConfigs");

            migrationBuilder.DropIndex(
                name: "UX_OrderDisplaySequences_Tenant_Code_Bucket",
                table: "OrderDisplaySequences");

            migrationBuilder.DropColumn(
                name: "BucketKey",
                table: "OrderDisplaySequences");

            migrationBuilder.AlterColumn<string>(
                name: "OrderTypeCode",
                table: "OrderDisplaySequences",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.CreateIndex(
                name: "UX_OrderDisplaySequences_Tenant_Code_Year_Month",
                table: "OrderDisplaySequences",
                columns: new[] { "TenantId", "OrderTypeCode", "Year", "Month" },
                unique: true);
        }
    }
}
