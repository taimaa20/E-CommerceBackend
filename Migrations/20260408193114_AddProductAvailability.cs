using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AvailableEndDate",
                table: "Products",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "AvailableFrom",
                table: "Products",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AvailableStartDate",
                table: "Products",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "AvailableTo",
                table: "Products",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvailableEndDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailableFrom",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailableStartDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AvailableTo",
                table: "Products");
        }
    }
}
