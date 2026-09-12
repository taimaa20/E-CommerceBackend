using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPhoneOtpAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "CustomerAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "CustomerAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsProfileCompleted",
                table: "CustomerAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "CustomerAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerOtps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OtpCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOtps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOtps_CustomerAccounts_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccounts_Tenant_PhoneNumber",
                table: "CustomerAccounts",
                columns: new[] { "TenantId", "PhoneNumber" },
                unique: true,
                filter: "\"PhoneNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOtps_CustomerId",
                table: "CustomerOtps",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOtps_Tenant_Customer_Purpose_Used",
                table: "CustomerOtps",
                columns: new[] { "TenantId", "CustomerId", "Purpose", "UsedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOtps_Tenant_Phone_Created",
                table: "CustomerOtps",
                columns: new[] { "TenantId", "PhoneNumber", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerOtps");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccounts_Tenant_PhoneNumber",
                table: "CustomerAccounts");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "CustomerAccounts");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "CustomerAccounts");

            migrationBuilder.DropColumn(
                name: "IsProfileCompleted",
                table: "CustomerAccounts");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "CustomerAccounts");
        }
    }
}
