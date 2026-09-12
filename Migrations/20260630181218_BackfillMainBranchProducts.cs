using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMainBranchProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "BranchProducts" (
                    "Id",
                    "BranchId",
                    "ProductId",
                    "IsAvailable",
                    "IsVisible",
                    "DisplayOrder",
                    "CreatedById",
                    "UpdatedById",
                    "TenantId",
                    "CreatedAt",
                    "UpdatedAt",
                    "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id",
                    p."Id",
                    TRUE,
                    TRUE,
                    0,
                    NULL,
                    NULL,
                    p."TenantId",
                    NOW(),
                    NOW(),
                    NULL
                FROM "Products" p
                INNER JOIN "Branches" b
                    ON b."TenantId" = p."TenantId"
                    AND b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                CROSS JOIN LATERAL (
                    SELECT md5(p."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE p."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchProducts" existing
                        WHERE existing."TenantId" = p."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."ProductId" = p."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "BranchProducts" bp
                USING "Products" p,
                    "Branches" b,
                    LATERAL (
                    SELECT md5(p."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE bp."ProductId" = p."Id"
                    AND bp."BranchId" = b."Id"
                    AND bp."Id" = CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid);
                """);
        }
    }
}
