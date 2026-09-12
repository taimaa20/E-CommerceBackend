//using Microsoft.EntityFrameworkCore.Infrastructure;
//using Microsoft.EntityFrameworkCore.Migrations;
//using RestaurantPos.Api.Data;

//#nullable disable

//namespace RestaurantPos.Api.Migrations
//{
//    // PosDbContext seeds the default payment methods via HasData, but the
//    // migration that materialized that seed was never registered with EF, and
//    // the later AddCostProfitabilityEngine migration only UpdateData's the rows
//    // (a silent 0-row no-op when they don't exist). Fresh databases therefore
//    // come up with an empty PaymentMethods table and the whole checkout flow is
//    // dead. Re-seed idempotently: existing environments hit the NOT EXISTS
//    // guard and nothing changes.
//    [DbContext(typeof(PosDbContext))]
//    [Migration("20260702020000_SeedDefaultPaymentMethods")]
//    public partial class SeedDefaultPaymentMethods : Migration
//    {
//        /// <inheritdoc />
//        protected override void Up(MigrationBuilder migrationBuilder)
//        {
//            migrationBuilder.Sql("""
//                INSERT INTO "PaymentMethods" (
//                    "Id", "TenantId", "NameEn", "NameAr", "Code", "DisplayOrder",
//                    "IsActive", "IsDefault", "RequiresReferenceNumber",
//                    "CostSharingCommissionPercentage", "CostSharingCounterpartyPercentage",
//                    "CostSharingMode", "CostSharingRestaurantPercentage", "CostSharingScope",
//                    "CreatedAt", "UpdatedAt")
//                SELECT
//                    seed."Id"::uuid,
//                    '3fa85f64-5717-4562-b3fc-2c963f66afa6'::uuid,
//                    seed."NameEn", seed."NameAr", seed."Code", seed."DisplayOrder",
//                    TRUE, seed."IsDefault", FALSE,
//                    0, 0, 0, 0, 0,
//                    now(), now()
//                FROM (VALUES
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d501', 'Cash', 'نقدي', 'CASH', 1, TRUE),
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d502', 'Visa / Mastercard', 'فيزا / ماستركارد', 'VISA_MASTERCARD', 2, FALSE),
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d503', 'InstaPay', 'إنستاباي', 'INSTAPAY', 3, FALSE),
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d504', 'Vodafone Cash', 'فودافون كاش', 'VODAFONE_CASH', 4, FALSE),
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d505', 'Etisalat Cash', 'اتصالات كاش', 'ETISALAT_CASH', 5, FALSE),
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d506', 'Orange Cash', 'أورنج كاش', 'ORANGE_CASH', 6, FALSE),
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d507', 'Meeza', 'ميزة', 'MEEZA', 7, FALSE),
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d508', 'CliQ', 'كليك', 'CLIQ', 8, FALSE),
//                    ('1ab8e74c-9912-42fa-9c1d-56d6f8f2d509', 'Bank Transfer', 'تحويل بنكي', 'BANK_TRANSFER', 9, FALSE)
//                ) AS seed("Id", "NameEn", "NameAr", "Code", "DisplayOrder", "IsDefault")
//                WHERE NOT EXISTS (
//                    SELECT 1 FROM "PaymentMethods" existing
//                    WHERE existing."Id" = seed."Id"::uuid
//                );

//                -- Enable the seeded methods for each tenant's Main Branch — the same
//                -- backfill AddMasterDataBranchConfigurations performed for methods
//                -- that existed at its time (BranchPaymentMethods has no rows for
//                -- migration-seeded methods; without them /PaymentMethods/active is
//                -- empty and checkout is impossible on a fresh environment).
//                INSERT INTO "BranchPaymentMethods" (
//                    "Id", "BranchId", "PaymentMethodId", "IsEnabled",
//                    "CreatedById", "UpdatedById", "TenantId", "CreatedAt", "UpdatedAt", "DeletedAt")
//                SELECT
//                    CAST(
//                        SUBSTRING(h."Hash", 1, 8) || '-' ||
//                        SUBSTRING(h."Hash", 9, 4) || '-' ||
//                        SUBSTRING(h."Hash", 13, 4) || '-' ||
//                        SUBSTRING(h."Hash", 17, 4) || '-' ||
//                        SUBSTRING(h."Hash", 21, 12)
//                        AS uuid),
//                    b."Id", pm."Id", TRUE,
//                    NULL, NULL, pm."TenantId", NOW(), NOW(), NULL
//                FROM "PaymentMethods" pm
//                INNER JOIN "Branches" b
//                    ON b."TenantId" = pm."TenantId"
//                    AND b."IsMainBranch" = TRUE
//                    AND b."DeletedAt" IS NULL
//                CROSS JOIN LATERAL (
//                    SELECT md5(pm."Id"::text || ':' || b."Id"::text) AS "Hash"
//                ) h
//                WHERE pm."DeletedAt" IS NULL
//                    AND NOT EXISTS (
//                        SELECT 1
//                        FROM "BranchPaymentMethods" existing
//                        WHERE existing."TenantId" = pm."TenantId"
//                            AND existing."BranchId" = b."Id"
//                            AND existing."PaymentMethodId" = pm."Id"
//                            AND existing."DeletedAt" IS NULL
//                    )
//                ON CONFLICT DO NOTHING;
//                """);
//        }

//        /// <inheritdoc />
//        protected override void Down(MigrationBuilder migrationBuilder)
//        {
//            // Seed-only repair: rows may have been edited by operators since;
//            // removing them on rollback would destroy live configuration.
//        }
//    }
//}
