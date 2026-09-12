using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWasteLogStaffMealEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WasteLogEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WasteLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeNameSnapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WasteLogEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WasteLogEmployees_Users_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WasteLogEmployees_WasteLogs_WasteLogId",
                        column: x => x.WasteLogId,
                        principalTable: "WasteLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogEmployees_EmployeeId",
                table: "WasteLogEmployees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogEmployees_WasteLogId",
                table: "WasteLogEmployees",
                column: "WasteLogId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogEmployees_TenantId_EmployeeId_CreatedAt",
                table: "WasteLogEmployees",
                columns: new[] { "TenantId", "EmployeeId", "CreatedAt" },
                filter: "\"EmployeeId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WasteLogEmployees");
        }
    }
}
