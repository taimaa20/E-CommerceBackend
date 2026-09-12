using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchOwnershipZonesAssetsOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_DeliveryZones_TenantId_Code";
                DROP INDEX IF EXISTS "IX_DeliveryZones_TenantId_IsActive_DisplayOrder";
                """);

            // Same pattern as AddBranchIdToTables: add nullable, backfill every existing row
            // to its tenant's Main Branch (legacy single-branch data belonged there), verify,
            // then lock the column down. Idempotent so partially-patched DBs converge.
            migrationBuilder.Sql("""
                ALTER TABLE "DeliveryZones" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "Assets" ADD COLUMN IF NOT EXISTS "BranchId" uuid;

                WITH main_branches AS (
                    SELECT "TenantId", "Id"
                    FROM "Branches"
                    WHERE "IsMainBranch" = TRUE
                      AND "IsActive" = TRUE
                      AND "DeletedAt" IS NULL
                )
                UPDATE "DeliveryZones" target
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
                UPDATE "Assets" target
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
                        SELECT 'DeliveryZones' AS "TableName" WHERE EXISTS (SELECT 1 FROM "DeliveryZones" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'Assets' WHERE EXISTS (SELECT 1 FROM "Assets" WHERE "BranchId" IS NULL)
                    ) failed;

                    IF missing_tables IS NOT NULL THEN
                        RAISE EXCEPTION 'DeliveryZone/Asset BranchId backfill failed for tables: %', missing_tables;
                    END IF;
                END $$;

                ALTER TABLE "DeliveryZones" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "Assets" ALTER COLUMN "BranchId" SET NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "BranchOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchOffers_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchOffers_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_BranchId",
                table: "DeliveryZones",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_TenantId_BranchId_Code",
                table: "DeliveryZones",
                columns: new[] { "TenantId", "BranchId", "Code" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_TenantId_BranchId_IsActive_DisplayOrder",
                table: "DeliveryZones",
                columns: new[] { "TenantId", "BranchId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_BranchId",
                table: "Assets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Tenant_Branch_Status",
                table: "Assets",
                columns: new[] { "TenantId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchOffers_BranchId",
                table: "BranchOffers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchOffers_OfferId",
                table: "BranchOffers",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchOffers_Tenant_Branch_Offer",
                table: "BranchOffers",
                columns: new[] { "TenantId", "BranchId", "OfferId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BranchOffers_Tenant_Offer",
                table: "BranchOffers",
                columns: new[] { "TenantId", "OfferId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Branches_BranchId",
                table: "Assets",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryZones_Branches_BranchId",
                table: "DeliveryZones",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Enable every existing offer on its tenant's Main Branch (deterministic md5
            // ids, same shape as AddMasterDataBranchConfigurations) so Main Branch keeps
            // its legacy offer behavior; other branches opt in via Branch Configuration.
            // Offers carry no TenantId, so the row inherits the Main Branch's tenant.
            migrationBuilder.Sql("""
                INSERT INTO "BranchOffers" (
                    "Id", "BranchId", "OfferId", "IsEnabled",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", o."Id", TRUE,
                    NULL, NULL, b."TenantId", NOW(), NOW(), NULL
                FROM "Offers" o
                CROSS JOIN "Branches" b
                CROSS JOIN LATERAL (
                    SELECT md5(o."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchOffers" existing
                        WHERE existing."BranchId" = b."Id"
                            AND existing."OfferId" = o."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Branches_BranchId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryZones_Branches_BranchId",
                table: "DeliveryZones");

            migrationBuilder.DropTable(
                name: "BranchOffers");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryZones_BranchId",
                table: "DeliveryZones");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryZones_TenantId_BranchId_Code",
                table: "DeliveryZones");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryZones_TenantId_BranchId_IsActive_DisplayOrder",
                table: "DeliveryZones");

            migrationBuilder.DropIndex(
                name: "IX_Assets_BranchId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_Tenant_Branch_Status",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Assets");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_TenantId_Code",
                table: "DeliveryZones",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_TenantId_IsActive_DisplayOrder",
                table: "DeliveryZones",
                columns: new[] { "TenantId", "IsActive", "DisplayOrder" });
        }
    }
}
