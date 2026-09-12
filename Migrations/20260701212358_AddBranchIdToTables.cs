using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Tables" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "TableCategories" ADD COLUMN IF NOT EXISTS "BranchId" uuid;

                WITH main_branches AS (
                    SELECT "TenantId", "Id"
                    FROM "Branches"
                    WHERE "IsMainBranch" = TRUE
                      AND "IsActive" = TRUE
                      AND "DeletedAt" IS NULL
                )
                UPDATE "TableCategories" target
                SET "BranchId" = main_branches."Id"
                FROM main_branches
                WHERE target."BranchId" IS NULL
                  AND target."TenantId" = main_branches."TenantId";

                WITH main_branches AS (
                    SELECT "TenantId", "Id"
                    FROM "Branches"
                    WHERE "IsMainBranch" = TRUE
                      AND "IsActive" = TRUE
                      AND "DeletedAt" IS NULL
                )
                UPDATE "Tables" target
                SET "BranchId" = main_branches."Id"
                FROM main_branches
                WHERE target."BranchId" IS NULL
                  AND target."TenantId" = main_branches."TenantId";

                DO $$
                DECLARE missing_tables text;
                BEGIN
                    SELECT string_agg("TableName", ', ' ORDER BY "TableName")
                    INTO missing_tables
                    FROM (
                        SELECT 'Tables' AS "TableName" WHERE EXISTS (SELECT 1 FROM "Tables" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'TableCategories' WHERE EXISTS (SELECT 1 FROM "TableCategories" WHERE "BranchId" IS NULL)
                    ) failed;

                    IF missing_tables IS NOT NULL THEN
                        RAISE EXCEPTION 'Table/TableCategory BranchId backfill failed for tables: %', missing_tables;
                    END IF;
                END $$;

                ALTER TABLE "Tables" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "TableCategories" ALTER COLUMN "BranchId" SET NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_BranchId",
                table: "Tables",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_Tenant_Branch_Name",
                table: "Tables",
                columns: new[] { "TenantId", "BranchId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_TableCategories_BranchId",
                table: "TableCategories",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TableCategories_Tenant_Branch_Name",
                table: "TableCategories",
                columns: new[] { "TenantId", "BranchId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_TableCategories_Branches_BranchId",
                table: "TableCategories",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_Branches_BranchId",
                table: "Tables",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TableCategories_Branches_BranchId",
                table: "TableCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_Branches_BranchId",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_BranchId",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_Tenant_Branch_Name",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_TableCategories_BranchId",
                table: "TableCategories");

            migrationBuilder.DropIndex(
                name: "IX_TableCategories_Tenant_Branch_Name",
                table: "TableCategories");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "TableCategories");
        }
    }
}
