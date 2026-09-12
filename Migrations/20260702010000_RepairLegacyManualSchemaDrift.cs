using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantPos.Api.Data;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    // Several early hand-written migrations (AddOfferDescriptions, the SystemSettings
    // social-link columns, AddKitchenOrdersIndex) were never registered with EF —
    // no Designer/[Migration] attribute — so their SQL was only ever run manually
    // against the existing environments. Any database created from the migration
    // chain alone comes up without these columns and every query projecting them
    // fails (Offers list, Orders joins, SystemSettings load). This repair migration
    // reconciles fresh databases; on databases where the columns already exist it
    // is a complete no-op (IF NOT EXISTS everywhere).
    [DbContext(typeof(PosDbContext))]
    [Migration("20260702010000_RepairLegacyManualSchemaDrift")]
    public partial class RepairLegacyManualSchemaDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Offers" ADD COLUMN IF NOT EXISTS "Description" character varying(1000) NULL;
                ALTER TABLE "Offers" ADD COLUMN IF NOT EXISTS "DescriptionAr" character varying(1000) NULL;

                ALTER TABLE "SystemSettings" ADD COLUMN IF NOT EXISTS "FacebookUrl" character varying(500) NULL;
                ALTER TABLE "SystemSettings" ADD COLUMN IF NOT EXISTS "InstagramUrl" character varying(500) NULL;
                ALTER TABLE "SystemSettings" ADD COLUMN IF NOT EXISTS "TikTokUrl" character varying(500) NULL;
                ALTER TABLE "SystemSettings" ADD COLUMN IF NOT EXISTS "GoogleMapsLocationUrl" character varying(500) NULL;
                ALTER TABLE "SystemSettings" ADD COLUMN IF NOT EXISTS "GoogleReviewUrl" character varying(500) NULL;

                CREATE INDEX IF NOT EXISTS "IX_Orders_Kitchen" ON "Orders" ("TenantId", "Status", "CreatedAt");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Orders_Kitchen";

                ALTER TABLE "SystemSettings" DROP COLUMN IF EXISTS "GoogleReviewUrl";
                ALTER TABLE "SystemSettings" DROP COLUMN IF EXISTS "GoogleMapsLocationUrl";
                ALTER TABLE "SystemSettings" DROP COLUMN IF EXISTS "TikTokUrl";
                ALTER TABLE "SystemSettings" DROP COLUMN IF EXISTS "InstagramUrl";
                ALTER TABLE "SystemSettings" DROP COLUMN IF EXISTS "FacebookUrl";

                ALTER TABLE "Offers" DROP COLUMN IF EXISTS "DescriptionAr";
                ALTER TABLE "Offers" DROP COLUMN IF EXISTS "Description";
                """);
        }
    }
}
