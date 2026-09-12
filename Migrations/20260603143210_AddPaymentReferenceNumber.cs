using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReferenceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodCode",
                table: "Payments",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentMethodId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodName",
                table: "Payments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodNameAr",
                table: "Payments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "Payments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NameEn = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Icon = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresReferenceNumber = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                });

           
            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentMethodId",
                table: "Payments",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Tenant_PaymentMethod_CreatedAt",
                table: "Payments",
                columns: new[] { "TenantId", "PaymentMethodId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Tenant_Active_Order",
                table: "PaymentMethods",
                columns: new[] { "TenantId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Tenant_Code",
                table: "PaymentMethods",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Tenant_Default",
                table: "PaymentMethods",
                columns: new[] { "TenantId", "IsDefault" },
                unique: true,
                filter: "\"IsDefault\" = true AND \"DeletedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentMethods_PaymentMethodId",
                table: "Payments",
                column: "PaymentMethodId",
                principalTable: "PaymentMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE IF EXISTS \"Payments\" DROP CONSTRAINT IF EXISTS \"FK_Payments_PaymentMethods_PaymentMethodId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Payments_PaymentMethodId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Payments_Tenant_PaymentMethod_CreatedAt\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"PaymentMethods\";");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS \"Payments\" DROP COLUMN IF EXISTS \"ReferenceNumber\";");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS \"Payments\" DROP COLUMN IF EXISTS \"PaymentMethodCode\";");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS \"Payments\" DROP COLUMN IF EXISTS \"PaymentMethodId\";");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS \"Payments\" DROP COLUMN IF EXISTS \"PaymentMethodName\";");
            migrationBuilder.Sql("ALTER TABLE IF EXISTS \"Payments\" DROP COLUMN IF EXISTS \"PaymentMethodNameAr\";");
        }
    }
}
