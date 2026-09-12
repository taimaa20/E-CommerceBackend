using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "SystemSettings"
                ADD COLUMN IF NOT EXISTS "TimeZoneId" character varying(100) NOT NULL DEFAULT 'Asia/Qatar';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "SystemSettings"
                DROP COLUMN IF EXISTS "TimeZoneId";
                """);
        }
    }
}
