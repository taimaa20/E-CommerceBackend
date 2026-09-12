using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsMainBranch = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.CheckConstraint("CK_Branches_Code_Normalized", "\"Code\" = upper(replace(btrim(\"Code\"), ' ', '_')) AND length(\"Code\") > 0");
                    table.CheckConstraint("CK_Branches_Name_Normalized", "\"Name\" = btrim(\"Name\") AND length(\"Name\") > 0");
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Address", "Code", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "Description", "Email", "IsActive", "IsMainBranch", "Name", "NameAr", "Phone", "TenantId", "UpdatedAt", "UpdatedBy", "UpdatedById" },
                values: new object[] { new Guid("0d5d315d-4bb4-4dc3-9ac8-c6ef4fcf0f01"), null, "MAIN", new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc), null, null, null, null, null, true, true, "Main Branch", "الفرع الرئيسي", null, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), new DateTime(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc), null, null });

            migrationBuilder.Sql("""
                INSERT INTO "Branches" (
                    "Id",
                    "Name",
                    "NameAr",
                    "Code",
                    "IsMainBranch",
                    "IsActive",
                    "TenantId",
                    "CreatedAt",
                    "UpdatedAt"
                )
                SELECT
                    md5(t."Id"::text || ':main-branch')::uuid,
                    LEFT(COALESCE(NULLIF(TRIM(t."BusinessName"), ''), NULLIF(TRIM(t."Name"), ''), 'Main Branch'), 120),
                    'الفرع الرئيسي',
                    'MAIN',
                    TRUE,
                    TRUE,
                    t."Id",
                    now(),
                    now()
                FROM "Tenants" t
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Branches" b
                    WHERE b."TenantId" = t."Id"
                      AND b."DeletedAt" IS NULL
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Tenant_Active_Name",
                table: "Branches",
                columns: new[] { "TenantId", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Tenant_Code",
                table: "Branches",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Tenant_MainBranch",
                table: "Branches",
                columns: new[] { "TenantId", "IsMainBranch" },
                unique: true,
                filter: "\"IsMainBranch\" = TRUE AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Tenant_Name",
                table: "Branches",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Branches");
        }
    }
}
