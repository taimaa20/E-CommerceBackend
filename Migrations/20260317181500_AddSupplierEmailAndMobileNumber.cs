using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierEmailAndMobileNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobileNumber",
                table: "Suppliers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Migrate existing ContactInfo to MobileNumber if ContactInfo exists and MobileNumber is null
            migrationBuilder.Sql(@"
                UPDATE ""Suppliers""
                SET ""MobileNumber"" = ""ContactInfo""
                WHERE ""MobileNumber"" IS NULL AND ""ContactInfo"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "MobileNumber",
                table: "Suppliers");
        }
    }
}
