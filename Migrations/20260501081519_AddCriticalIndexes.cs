using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCriticalIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2647), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2647) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2641), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2643) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2650), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2650) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2649), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2649) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2651), new DateTime(2026, 5, 1, 8, 15, 18, 694, DateTimeKind.Utc).AddTicks(2651) });

            // Fresh-database repair: 20260313145030 inserted one seed-user set and
            // 20260407210304 deleted a *different* (stale) set of ids before inserting
            // the canonical SeedData users, so on a brand-new database both generations
            // survive and the unique (TenantId, Username) index below fails. Databases
            // that already ran this migration are untouched (EF never re-runs it).
            // The orphan generation is migration-seeded only — nothing references it.
            migrationBuilder.Sql("""
                DELETE FROM "Users" stale
                WHERE stale."Id" IN (
                    '1814c0e7-ee99-4b24-af63-542a431b2631',
                    '579ee241-626c-42de-a372-d210af851c65',
                    '9c4dd8f9-b402-4f26-830e-c7f75911c7cd',
                    'ef564ee0-d4f7-4a06-9a7f-b6d9c0e2a3d9')
                  AND EXISTS (
                    SELECT 1 FROM "Users" canonical
                    WHERE canonical."TenantId" = stale."TenantId"
                      AND canonical."Username" = stale."Username"
                      AND canonical."Id" <> stale."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Tenant_Username",
                table: "Users",
                columns: new[] { "TenantId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockBatches_Tenant_Status_Expiry",
                table: "StockBatches",
                columns: new[] { "TenantId", "Status", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Tenant_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "TenantId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Tenant_Phone",
                table: "Customers",
                columns: new[] { "TenantId", "PhoneNumber" },
                unique: true,
                filter: "\"PhoneNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Tenant_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_StockBatches_Tenant_Status_Expiry",
                table: "StockBatches");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_Tenant_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Tenant_Phone",
                table: "Customers");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("24594ed6-0fb7-4d03-8ae7-5d75414e72ec"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(769), new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(770) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("81084756-6e49-4865-be2c-5378d4e79ac8"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(761), new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(765) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("aa0a5731-df34-4688-89da-9d52e083d15c"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(774), new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(775) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d035fbeb-52e3-4404-b287-14df85e3b93e"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(772), new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(772) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f92f4ca4-9172-4b2f-afab-62fef87d94e0"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(776), new DateTime(2026, 5, 1, 8, 8, 38, 249, DateTimeKind.Utc).AddTicks(777) });
        }
    }
}
