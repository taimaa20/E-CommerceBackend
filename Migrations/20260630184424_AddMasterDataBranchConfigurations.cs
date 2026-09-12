using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataBranchConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchProductOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductOptionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_BranchProductOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchProductOptions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchProductOptions_ProductOptions_ProductOptionId",
                        column: x => x.ProductOptionId,
                        principalTable: "ProductOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchSubcategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcategoryId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_BranchSubcategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchSubcategories_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchSubcategories_Subcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchProductOptions_BranchId",
                table: "BranchProductOptions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchProductOptions_ProductOptionId",
                table: "BranchProductOptions",
                column: "ProductOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchProductOptions_Tenant_Branch_Option",
                table: "BranchProductOptions",
                columns: new[] { "TenantId", "BranchId", "ProductOptionId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BranchProductOptions_Tenant_Branch_Order",
                table: "BranchProductOptions",
                columns: new[] { "TenantId", "BranchId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchProductOptions_Tenant_Option",
                table: "BranchProductOptions",
                columns: new[] { "TenantId", "ProductOptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchSubcategories_BranchId",
                table: "BranchSubcategories",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchSubcategories_SubcategoryId",
                table: "BranchSubcategories",
                column: "SubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchSubcategories_Tenant_Branch_Order",
                table: "BranchSubcategories",
                columns: new[] { "TenantId", "BranchId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchSubcategories_Tenant_Branch_Subcategory",
                table: "BranchSubcategories",
                columns: new[] { "TenantId", "BranchId", "SubcategoryId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BranchSubcategories_Tenant_Subcategory",
                table: "BranchSubcategories",
                columns: new[] { "TenantId", "SubcategoryId" });

            migrationBuilder.Sql("""
                INSERT INTO "BranchCategories" (
                    "Id", "BranchId", "CategoryId", "IsVisible", "DisplayOrder",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", c."Id", TRUE, c."SortOrder",
                    NULL, NULL, c."TenantId", NOW(), NOW(), NULL
                FROM "Categories" c
                INNER JOIN "Branches" b
                    ON b."TenantId" = c."TenantId"
                    AND b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                CROSS JOIN LATERAL (
                    SELECT md5(c."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE c."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchCategories" existing
                        WHERE existing."TenantId" = c."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."CategoryId" = c."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;

                INSERT INTO "BranchSubcategories" (
                    "Id", "BranchId", "SubcategoryId", "IsVisible", "DisplayOrder",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", s."Id", TRUE, s."DisplayOrder",
                    NULL, NULL, s."TenantId", NOW(), NOW(), NULL
                FROM "Subcategories" s
                INNER JOIN "Branches" b
                    ON b."TenantId" = s."TenantId"
                    AND b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                CROSS JOIN LATERAL (
                    SELECT md5(s."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE s."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchSubcategories" existing
                        WHERE existing."TenantId" = s."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."SubcategoryId" = s."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;

                INSERT INTO "BranchModifiers" (
                    "Id", "BranchId", "ModifierId", "IsAvailable",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", m."Id", TRUE,
                    NULL, NULL, m."TenantId", NOW(), NOW(), NULL
                FROM "Modifiers" m
                INNER JOIN "Branches" b
                    ON b."TenantId" = m."TenantId"
                    AND b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                CROSS JOIN LATERAL (
                    SELECT md5(m."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE m."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchModifiers" existing
                        WHERE existing."TenantId" = m."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."ModifierId" = m."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;

                INSERT INTO "BranchProductOptions" (
                    "Id", "BranchId", "ProductOptionId", "IsAvailable", "IsVisible", "DisplayOrder",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", o."Id", TRUE, TRUE, o."SortOrder",
                    NULL, NULL, o."TenantId", NOW(), NOW(), NULL
                FROM "ProductOptions" o
                INNER JOIN "Branches" b
                    ON b."TenantId" = o."TenantId"
                    AND b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                CROSS JOIN LATERAL (
                    SELECT md5(o."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE o."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchProductOptions" existing
                        WHERE existing."TenantId" = o."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."ProductOptionId" = o."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;

                INSERT INTO "BranchPaymentMethods" (
                    "Id", "BranchId", "PaymentMethodId", "IsEnabled",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", pm."Id", TRUE,
                    NULL, NULL, pm."TenantId", NOW(), NOW(), NULL
                FROM "PaymentMethods" pm
                INNER JOIN "Branches" b
                    ON b."TenantId" = pm."TenantId"
                    AND b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                CROSS JOIN LATERAL (
                    SELECT md5(pm."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE pm."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchPaymentMethods" existing
                        WHERE existing."TenantId" = pm."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."PaymentMethodId" = pm."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;

                INSERT INTO "BranchDeliveryPartners" (
                    "Id", "BranchId", "DeliveryPartnerId", "IsEnabled",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", dp."Id", TRUE,
                    NULL, NULL, dp."TenantId", NOW(), NOW(), NULL
                FROM "DeliveryPartners" dp
                INNER JOIN "Branches" b
                    ON b."TenantId" = dp."TenantId"
                    AND b."IsMainBranch" = TRUE
                    AND b."DeletedAt" IS NULL
                CROSS JOIN LATERAL (
                    SELECT md5(dp."Id"::text || ':' || b."Id"::text) AS "Hash"
                ) h
                WHERE dp."DeletedAt" IS NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM "BranchDeliveryPartners" existing
                        WHERE existing."TenantId" = dp."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."DeliveryPartnerId" = dp."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;

                INSERT INTO "BranchPrinters" (
                    "Id", "BranchId", "PrinterId", "IsEnabled", "DisplayOrder",
                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
                SELECT
                    CAST(
                        SUBSTRING(h."Hash", 1, 8) || '-' ||
                        SUBSTRING(h."Hash", 9, 4) || '-' ||
                        SUBSTRING(h."Hash", 13, 4) || '-' ||
                        SUBSTRING(h."Hash", 17, 4) || '-' ||
                        SUBSTRING(h."Hash", 21, 12)
                        AS uuid),
                    b."Id", p."Id", TRUE, 0,
                    NULL, NULL, p."TenantId", NOW(), NOW(), NULL
                FROM "Printers" p
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
                        FROM "BranchPrinters" existing
                        WHERE existing."TenantId" = p."TenantId"
                            AND existing."BranchId" = b."Id"
                            AND existing."PrinterId" = p."Id"
                            AND existing."DeletedAt" IS NULL
                    )
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "BranchCategories" config
                WHERE config."Id" = CAST(
                    SUBSTRING(md5(config."CategoryId"::text || ':' || config."BranchId"::text), 1, 8) || '-' ||
                    SUBSTRING(md5(config."CategoryId"::text || ':' || config."BranchId"::text), 9, 4) || '-' ||
                    SUBSTRING(md5(config."CategoryId"::text || ':' || config."BranchId"::text), 13, 4) || '-' ||
                    SUBSTRING(md5(config."CategoryId"::text || ':' || config."BranchId"::text), 17, 4) || '-' ||
                    SUBSTRING(md5(config."CategoryId"::text || ':' || config."BranchId"::text), 21, 12)
                    AS uuid);

                DELETE FROM "BranchModifiers" config
                WHERE config."Id" = CAST(
                    SUBSTRING(md5(config."ModifierId"::text || ':' || config."BranchId"::text), 1, 8) || '-' ||
                    SUBSTRING(md5(config."ModifierId"::text || ':' || config."BranchId"::text), 9, 4) || '-' ||
                    SUBSTRING(md5(config."ModifierId"::text || ':' || config."BranchId"::text), 13, 4) || '-' ||
                    SUBSTRING(md5(config."ModifierId"::text || ':' || config."BranchId"::text), 17, 4) || '-' ||
                    SUBSTRING(md5(config."ModifierId"::text || ':' || config."BranchId"::text), 21, 12)
                    AS uuid);

                DELETE FROM "BranchPaymentMethods" config
                WHERE config."Id" = CAST(
                    SUBSTRING(md5(config."PaymentMethodId"::text || ':' || config."BranchId"::text), 1, 8) || '-' ||
                    SUBSTRING(md5(config."PaymentMethodId"::text || ':' || config."BranchId"::text), 9, 4) || '-' ||
                    SUBSTRING(md5(config."PaymentMethodId"::text || ':' || config."BranchId"::text), 13, 4) || '-' ||
                    SUBSTRING(md5(config."PaymentMethodId"::text || ':' || config."BranchId"::text), 17, 4) || '-' ||
                    SUBSTRING(md5(config."PaymentMethodId"::text || ':' || config."BranchId"::text), 21, 12)
                    AS uuid);

                DELETE FROM "BranchDeliveryPartners" config
                WHERE config."Id" = CAST(
                    SUBSTRING(md5(config."DeliveryPartnerId"::text || ':' || config."BranchId"::text), 1, 8) || '-' ||
                    SUBSTRING(md5(config."DeliveryPartnerId"::text || ':' || config."BranchId"::text), 9, 4) || '-' ||
                    SUBSTRING(md5(config."DeliveryPartnerId"::text || ':' || config."BranchId"::text), 13, 4) || '-' ||
                    SUBSTRING(md5(config."DeliveryPartnerId"::text || ':' || config."BranchId"::text), 17, 4) || '-' ||
                    SUBSTRING(md5(config."DeliveryPartnerId"::text || ':' || config."BranchId"::text), 21, 12)
                    AS uuid);

                DELETE FROM "BranchPrinters" config
                WHERE config."Id" = CAST(
                    SUBSTRING(md5(config."PrinterId"::text || ':' || config."BranchId"::text), 1, 8) || '-' ||
                    SUBSTRING(md5(config."PrinterId"::text || ':' || config."BranchId"::text), 9, 4) || '-' ||
                    SUBSTRING(md5(config."PrinterId"::text || ':' || config."BranchId"::text), 13, 4) || '-' ||
                    SUBSTRING(md5(config."PrinterId"::text || ':' || config."BranchId"::text), 17, 4) || '-' ||
                    SUBSTRING(md5(config."PrinterId"::text || ':' || config."BranchId"::text), 21, 12)
                    AS uuid);
                """);

            migrationBuilder.DropTable(
                name: "BranchProductOptions");

            migrationBuilder.DropTable(
                name: "BranchSubcategories");
        }
    }
}
