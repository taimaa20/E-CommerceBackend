using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyAndWhatsAppContactLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LabelAr = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    MessageTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MessageTemplateAr = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppContacts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Id", "Code", "CreatedAt", "DeletedAt", "IsActive", "Name", "NameAr", "SortOrder", "Symbol", "TenantId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2101"), "JOD", new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc), null, true, "Jordanian Dinar", "دينار أردني", 1, "د.ا", new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc) },
                    { new Guid("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2102"), "USD", new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc), null, true, "US Dollar", "دولار أمريكي", 2, "$", new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc) },
                    { new Guid("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2103"), "QAR", new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc), null, true, "Qatari Riyal", "ريال قطري", 3, "ر.ق", new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc) },
                    { new Guid("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2104"), "SAR", new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc), null, true, "Saudi Riyal", "ريال سعودي", 4, "ر.س", new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc) },
                    { new Guid("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2105"), "EGP", new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc), null, true, "Egyptian Pound", "جنيه مصري", 5, "£", new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Tenant_Active_Sort",
                table: "Currencies",
                columns: new[] { "TenantId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Tenant_Code",
                table: "Currencies",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppContacts_Tenant_Purpose_Active_Sort",
                table: "WhatsAppContacts",
                columns: new[] { "TenantId", "Purpose", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "WhatsAppContacts");
        }
    }
}
