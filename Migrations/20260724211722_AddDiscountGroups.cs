using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultDiscountEnabled",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DefaultDiscountPercentage",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "CustomerDiscountPercentage",
                table: "Orders",
                newName: "DiscountGroupPercentage");

            migrationBuilder.RenameColumn(
                name: "CustomerDiscountAmount",
                table: "Orders",
                newName: "DiscountGroupAmount");

            migrationBuilder.AddColumn<Guid>(
                name: "DiscountGroupId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountGroupName",
                table: "Orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscountGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DiscountPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    VerificationNote = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountGroups", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscountGroups");

            migrationBuilder.DropColumn(
                name: "DiscountGroupId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountGroupName",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "DiscountGroupPercentage",
                table: "Orders",
                newName: "CustomerDiscountPercentage");

            migrationBuilder.RenameColumn(
                name: "DiscountGroupAmount",
                table: "Orders",
                newName: "CustomerDiscountAmount");

            migrationBuilder.AddColumn<bool>(
                name: "DefaultDiscountEnabled",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDiscountPercentage",
                table: "Customers",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
