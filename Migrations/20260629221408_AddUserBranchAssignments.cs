using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBranchAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserBranches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBranches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "UserBranches" (
                    "Id",
                    "UserId",
                    "BranchId",
                    "IsDefault",
                    "TenantId",
                    "CreatedAt",
                    "UpdatedAt"
                )
                SELECT
                    md5(u."Id"::text || ':' || b."Id"::text || ':user-branch')::uuid,
                    u."Id",
                    b."Id",
                    TRUE,
                    u."TenantId",
                    now(),
                    now()
                FROM "Users" u
                INNER JOIN "Branches" b
                    ON b."TenantId" = u."TenantId"
                   AND b."IsMainBranch" = TRUE
                   AND b."DeletedAt" IS NULL
                WHERE u."DeletedAt" IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "UserBranches" ub
                      WHERE ub."UserId" = u."Id"
                        AND ub."BranchId" = b."Id"
                        AND ub."DeletedAt" IS NULL
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_BranchId",
                table: "UserBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_Tenant_Branch",
                table: "UserBranches",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_Tenant_User_Branch",
                table: "UserBranches",
                columns: new[] { "TenantId", "UserId", "BranchId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_Tenant_User_Default",
                table: "UserBranches",
                columns: new[] { "TenantId", "UserId", "IsDefault" },
                unique: true,
                filter: "\"IsDefault\" = TRUE AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserBranches_UserId",
                table: "UserBranches",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBranches");
        }
    }
}
