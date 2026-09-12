using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDisplayNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayOrderNumber",
                table: "Orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderDisplaySequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderTypeCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDisplaySequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Tenant_DisplayOrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "DisplayOrderNumber" },
                unique: true,
                filter: "\"DisplayOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_OrderDisplaySequences_Tenant_Code_Year_Month",
                table: "OrderDisplaySequences",
                columns: new[] { "TenantId", "OrderTypeCode", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderDisplaySequences");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Tenant_DisplayOrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DisplayOrderNumber",
                table: "Orders");
        }
    }
}
