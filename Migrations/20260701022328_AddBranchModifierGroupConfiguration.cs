using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantPos.Api.Data;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    [DbContext(typeof(PosDbContext))]
    [Migration("20260701022328_AddBranchModifierGroupConfiguration")]
    public partial class AddBranchModifierGroupConfiguration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchModifierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchModifierGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchModifierGroups_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchModifierGroups_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchModifierGroups_BranchId",
                table: "BranchModifierGroups",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchModifierGroups_ModifierGroupId",
                table: "BranchModifierGroups",
                column: "ModifierGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchModifierGroups_Tenant_Branch_Group",
                table: "BranchModifierGroups",
                columns: new[] { "TenantId", "BranchId", "ModifierGroupId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BranchModifierGroups_Tenant_Branch_Order",
                table: "BranchModifierGroups",
                columns: new[] { "TenantId", "BranchId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchModifierGroups_Tenant_Group",
                table: "BranchModifierGroups",
                columns: new[] { "TenantId", "ModifierGroupId" });

            migrationBuilder.Sql("""
                INSERT INTO "BranchModifierGroups" (
                    "Id", "BranchId", "ModifierGroupId", "IsAvailable", "IsVisible", "DisplayOrder",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", g."Id", TRUE, TRUE, 0,
                    NULL, NULL, g."TenantId", NOW(), NOW(), NULL
                FROM "ModifierGroups" g
                INNER JOIN "Branches" b
                    ON b."TenantId" = g."TenantId"
                    AND b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                CROSS JOIN LATERAL (
                    SELECT md5(g."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE g."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchModifierGroups" existing
                        WHERE existing."TenantId" = g."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."ModifierGroupId" = g."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchModifierGroups");
        }
    }
}
