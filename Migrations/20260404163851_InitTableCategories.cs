using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitTableCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create TableCategories table if it doesn't exist (AddTableCategories migration
            // is missing its Designer.cs so EF may skip it; this makes the migration self-contained).
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""TableCategories"" (
                    ""Id"" uuid NOT NULL,
                    ""Name"" character varying(50) NOT NULL,
                    ""NameAr"" character varying(50) NULL,
                    ""Color"" character varying(30) NULL,
                    ""TenantId"" uuid NOT NULL,
                    CONSTRAINT ""PK_TableCategories"" PRIMARY KEY (""Id"")
                );

                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Tables' AND column_name = 'TableCategoryId'
                    ) THEN
                        ALTER TABLE ""Tables"" ADD COLUMN ""TableCategoryId"" uuid NULL;
                        CREATE INDEX ""IX_Tables_TableCategoryId"" ON ""Tables"" (""TableCategoryId"");
                        ALTER TABLE ""Tables"" ADD CONSTRAINT ""FK_Tables_TableCategories_TableCategoryId""
                            FOREIGN KEY (""TableCategoryId"") REFERENCES ""TableCategories"" (""Id"") ON DELETE SET NULL;
                    END IF;
                END $$;

                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE tablename = 'TableCategories' AND indexname = 'IX_TableCategories_TenantId'
                    ) THEN
                        CREATE INDEX ""IX_TableCategories_TenantId"" ON ""TableCategories"" (""TenantId"");
                    END IF;
                END $$;
            ");

            migrationBuilder.DropIndex(
                name: "IX_TableCategories_TenantId",
                table: "TableCategories");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("32747e45-d718-4baa-a93b-150b518263d1"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("44614dac-cac1-41c3-8fd8-1e2bc87320ed"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("d7bf6fe6-a11d-4ee3-ad09-d9cee7a0f00a"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("dd110473-7941-43df-876b-c87c57e4f178"));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("12575421-e8a9-47ee-8df5-ce6428ead050"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("13fa12fd-84c5-4903-852b-360eab237787"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" },
                    { new Guid("4cbb573b-82f2-4f08-9db1-5c2a931cd0da"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("b412a1d6-ca42-4fba-be58-18af05e6169e"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("12575421-e8a9-47ee-8df5-ce6428ead050"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("13fa12fd-84c5-4903-852b-360eab237787"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("4cbb573b-82f2-4f08-9db1-5c2a931cd0da"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b412a1d6-ca42-4fba-be58-18af05e6169e"));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CommissionRate", "FullName", "MonthlySalary", "PasswordHash", "Role", "TenantId", "Username" },
                values: new object[,]
                {
                    { new Guid("32747e45-d718-4baa-a93b-150b518263d1"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 0, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "admin" },
                    { new Guid("44614dac-cac1-41c3-8fd8-1e2bc87320ed"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 1, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "garson" },
                    { new Guid("d7bf6fe6-a11d-4ee3-ad09-d9cee7a0f00a"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 2, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cheif" },
                    { new Guid("dd110473-7941-43df-876b-c87c57e4f178"), 0m, null, 0m, "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7", 3, new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), "cashier" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TableCategories_TenantId",
                table: "TableCategories",
                column: "TenantId");
        }
    }
}
