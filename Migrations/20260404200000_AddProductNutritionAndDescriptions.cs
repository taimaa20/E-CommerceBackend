using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantPos.Api.Data;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    // Hand-written migration that was missing its Designer/[Migration] attribute,
    // so EF never discovered or applied it: fresh databases came up without
    // Products.Description/DescriptionAr/Calories and every query projecting them
    // failed. Registering it is safe for existing databases — the SQL is
    // ADD/DROP COLUMN IF (NOT) EXISTS, so re-application is a no-op there.
    [DbContext(typeof(PosDbContext))]
    [Migration("20260404200000_AddProductNutritionAndDescriptions")]
    public partial class AddProductNutritionAndDescriptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""Description"" character varying(1000) NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""DescriptionAr"" character varying(1000) NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""Calories"" integer NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" DROP COLUMN IF EXISTS ""Calories"";
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" DROP COLUMN IF EXISTS ""DescriptionAr"";
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Products"" DROP COLUMN IF EXISTS ""Description"";
            ");
        }
    }
}
