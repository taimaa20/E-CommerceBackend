using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalBranchAssignments : Migration
    {
        private static readonly string[] NewBranchColumnTables =
        {
            "StockBatches",
            "RefundLogs",
            "RawMaterialInventories",
            "PurchaseOrders",
            "PurchaseOrderItems",
            "PrintJobs",
            "Payments",
            "PaymentAdjustments",
            "PaymentAdjustmentDetails",
            "Orders",
            "OrderItems",
            "Notifications",
            "InventoryTransfers",
            "InventoryTransactions",
            "CashierBalanceShifts",
            "CancelLogs"
        };

        private static readonly (string Table, string Name)[] BranchForeignKeys =
        {
            ("CancelLogs", "FK_CancelLogs_Branches_BranchId"),
            ("CashierBalanceShifts", "FK_CashierBalanceShifts_Branches_BranchId"),
            ("ExpenseInvoices", "FK_ExpenseInvoices_Branches_BranchId"),
            ("InventoryTransactions", "FK_InventoryTransactions_Branches_BranchId"),
            ("InventoryTransfers", "FK_InventoryTransfers_Branches_BranchId"),
            ("Notifications", "FK_Notifications_Branches_BranchId"),
            ("OrderItems", "FK_OrderItems_Branches_BranchId"),
            ("Orders", "FK_Orders_Branches_BranchId"),
            ("PaymentAdjustmentDetails", "FK_PaymentAdjustmentDetails_Branches_BranchId"),
            ("PaymentAdjustments", "FK_PaymentAdjustments_Branches_BranchId"),
            ("Payments", "FK_Payments_Branches_BranchId"),
            ("PrintJobs", "FK_PrintJobs_Branches_BranchId"),
            ("PurchaseOrderItems", "FK_PurchaseOrderItems_Branches_BranchId"),
            ("PurchaseOrders", "FK_PurchaseOrders_Branches_BranchId"),
            ("RawMaterialInventories", "FK_RawMaterialInventories_Branches_BranchId"),
            ("RefundLogs", "FK_RefundLogs_Branches_BranchId"),
            ("StockBatches", "FK_StockBatches_Branches_BranchId"),
            ("WasteLogs", "FK_WasteLogs_Branches_BranchId")
        };

        private static readonly (string Table, string Name, string[] Columns, bool Unique, string Filter)[] Indexes =
        {
            ("WasteLogs", "IX_WasteLogs_BranchId", new[] { "BranchId" }, false, null),
            ("WasteLogs", "IX_WasteLogs_Tenant_Branch_CreatedAt", new[] { "TenantId", "BranchId", "CreatedAt" }, false, null),
            ("WasteLogs", "IX_WasteLogs_Tenant_Branch_Status_CreatedAt", new[] { "TenantId", "BranchId", "Status", "CreatedAt" }, false, null),
            ("StockBatches", "IX_StockBatches_BranchId", new[] { "BranchId" }, false, null),
            ("StockBatches", "IX_StockBatches_Tenant_Branch_Material_Status_Expiry", new[] { "TenantId", "BranchId", "MaterialId", "Status", "ExpiryDate" }, false, null),
            ("RefundLogs", "IX_RefundLogs_BranchId", new[] { "BranchId" }, false, null),
            ("RefundLogs", "IX_RefundLogs_Tenant_Branch_ProcessedAt", new[] { "TenantId", "BranchId", "ProcessedAt" }, false, null),
            ("RawMaterialInventories", "IX_RawMaterialInventories_BranchId", new[] { "BranchId" }, false, null),
            ("RawMaterialInventories", "IX_RawMaterialInventories_Tenant_Branch_Material_Warehouse", new[] { "TenantId", "BranchId", "RawMaterialId", "WarehouseId" }, true, "\"DeletedAt\" IS NULL"),
            ("RawMaterialInventories", "IX_RawMaterialInventories_Tenant_Branch_Warehouse", new[] { "TenantId", "BranchId", "WarehouseId" }, false, null),
            ("PurchaseOrders", "IX_PurchaseOrders_BranchId", new[] { "BranchId" }, false, null),
            ("PurchaseOrders", "IX_PurchaseOrders_Tenant_Branch_Status_Date", new[] { "TenantId", "BranchId", "Status", "ExpectedDate" }, false, null),
            ("PurchaseOrders", "IX_PurchaseOrders_Tenant_Branch_Supplier", new[] { "TenantId", "BranchId", "SupplierId" }, false, null),
            ("PurchaseOrderItems", "IX_PurchaseOrderItems_BranchId", new[] { "BranchId" }, false, null),
            ("PurchaseOrderItems", "IX_PurchaseOrderItems_Tenant_Branch_Order", new[] { "TenantId", "BranchId", "PurchaseOrderId" }, false, null),
            ("PrintJobs", "IX_PrintJobs_BranchId", new[] { "BranchId" }, false, null),
            ("PrintJobs", "IX_PrintJobs_Tenant_Branch_Status_Next", new[] { "TenantId", "BranchId", "Status", "NextAttemptAt" }, false, null),
            ("Payments", "IX_Payments_BranchId", new[] { "BranchId" }, false, null),
            ("Payments", "IX_Payments_Tenant_Branch_CreatedAt", new[] { "TenantId", "BranchId", "CreatedAt" }, false, null),
            ("Payments", "IX_Payments_Tenant_Branch_Order_CreatedAt", new[] { "TenantId", "BranchId", "OrderId", "CreatedAt" }, false, null),
            ("PaymentAdjustments", "IX_PaymentAdjustments_BranchId", new[] { "BranchId" }, false, null),
            ("PaymentAdjustments", "IX_PaymentAdjustments_Tenant_Branch_Order_Status", new[] { "TenantId", "BranchId", "OrderId", "Status" }, false, null),
            ("PaymentAdjustmentDetails", "IX_PaymentAdjustmentDetails_BranchId", new[] { "BranchId" }, false, null),
            ("PaymentAdjustmentDetails", "IX_PaymentAdjustmentDetails_Tenant_Branch_Adjustment", new[] { "TenantId", "BranchId", "AdjustmentId" }, false, null),
            ("Orders", "IX_Orders_BranchId", new[] { "BranchId" }, false, null),
            ("Orders", "IX_Orders_Tenant_Branch_CreatedAt", new[] { "TenantId", "BranchId", "CreatedAt" }, false, null),
            ("Orders", "IX_Orders_Tenant_Branch_Status_CreatedAt", new[] { "TenantId", "BranchId", "Status", "CreatedAt" }, false, null),
            ("Orders", "IX_Orders_Tenant_Branch_Type_Status_CreatedAt", new[] { "TenantId", "BranchId", "OrderType", "Status", "CreatedAt" }, false, null),
            ("OrderItems", "IX_OrderItems_BranchId", new[] { "BranchId" }, false, null),
            ("OrderItems", "IX_OrderItems_Tenant_Branch_Order", new[] { "TenantId", "BranchId", "OrderId" }, false, null),
            ("OrderItems", "IX_OrderItems_Tenant_Branch_Product_CreatedAt", new[] { "TenantId", "BranchId", "ProductId", "CreatedAt" }, false, null),
            ("Notifications", "IX_Notifications_BranchId", new[] { "BranchId" }, false, null),
            ("Notifications", "IX_Notifications_Tenant_Branch_IsRead_CreatedAt", new[] { "TenantId", "BranchId", "IsRead", "CreatedAt" }, false, null),
            ("InventoryTransfers", "IX_InventoryTransfers_BranchId", new[] { "BranchId" }, false, null),
            ("InventoryTransfers", "IX_InventoryTransfers_Tenant_Branch_CreatedAt", new[] { "TenantId", "BranchId", "CreatedAt" }, false, null),
            ("InventoryTransactions", "IX_InventoryTransactions_BranchId", new[] { "BranchId" }, false, null),
            ("InventoryTransactions", "IX_InventoryTransactions_Tenant_Branch_Type_CreatedAt", new[] { "TenantId", "BranchId", "TransactionType", "CreatedAt" }, false, null),
            ("ExpenseInvoices", "IX_ExpenseInvoices_BranchId", new[] { "BranchId" }, false, null),
            ("ExpenseInvoices", "IX_ExpenseInvoices_Tenant_Branch_Date", new[] { "TenantId", "BranchId", "InvoiceDate" }, false, null),
            ("ExpenseInvoices", "IX_ExpenseInvoices_Tenant_Branch_Status_Date", new[] { "TenantId", "BranchId", "Status", "InvoiceDate" }, false, null),
            ("CashierBalanceShifts", "IX_CashierBalanceShifts_BranchId", new[] { "BranchId" }, false, null),
            ("CashierBalanceShifts", "IX_CashierBalanceShifts_Tenant_Branch_Cashier_OpenedAt", new[] { "TenantId", "BranchId", "CashierId", "OpenedAt" }, false, null),
            ("CancelLogs", "IX_CancelLogs_BranchId", new[] { "BranchId" }, false, null),
            ("CancelLogs", "IX_CancelLogs_Tenant_Branch_CancelledAt", new[] { "TenantId", "BranchId", "CancelledAt" }, false, null)
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_RawMaterialInventories_Tenant_Material_Warehouse";
                DROP INDEX IF EXISTS "IX_RawMaterialInventories_Tenant_Warehouse";

                ALTER TABLE "StockBatches" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "RefundLogs" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "RawMaterialInventories" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "PurchaseOrders" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "PurchaseOrderItems" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "PrintJobs" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "PaymentAdjustments" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "PaymentAdjustmentDetails" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "OrderItems" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "InventoryTransfers" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "InventoryTransactions" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "CashierBalanceShifts" ADD COLUMN IF NOT EXISTS "BranchId" uuid;
                ALTER TABLE "CancelLogs" ADD COLUMN IF NOT EXISTS "BranchId" uuid;

                ALTER TABLE "WasteLogs" ALTER COLUMN "BranchId" DROP NOT NULL;
                ALTER TABLE "ExpenseInvoices" ALTER COLUMN "BranchId" DROP NOT NULL;

                WITH inferred AS (
                    SELECT e."Id",
                           COALESCE(u."TenantId", s."TenantId") AS "TenantId"
                    FROM "ExpenseInvoices" e
                    LEFT JOIN "Users" u ON u."Id" = e."CreatedById"
                    LEFT JOIN "Suppliers" s ON s."Id" = e."SupplierId"
                    WHERE e."TenantId" = '00000000-0000-0000-0000-000000000000'
                      AND (u."TenantId" IS NOT NULL OR s."TenantId" IS NOT NULL)
                      AND (u."TenantId" IS NULL OR s."TenantId" IS NULL OR u."TenantId" = s."TenantId")
                )
                UPDATE "ExpenseInvoices" target
                SET "TenantId" = inferred."TenantId"
                FROM inferred
                WHERE target."Id" = inferred."Id";

                WITH notification_tenants AS (
                    SELECT "TenantId"
                    FROM "Notifications"
                    WHERE "TenantId" <> '00000000-0000-0000-0000-000000000000'
                    GROUP BY "TenantId"
                ),
                single_notification_tenant AS (
                    SELECT MIN("TenantId"::text)::uuid AS "TenantId"
                    FROM notification_tenants
                    HAVING COUNT(*) = 1
                )
                UPDATE "Notifications" target
                SET "TenantId" = single_notification_tenant."TenantId"
                FROM single_notification_tenant
                WHERE target."TenantId" = '00000000-0000-0000-0000-000000000000';

                WITH main_branches AS (
                    SELECT "TenantId", "Id"
                    FROM "Branches"
                    WHERE "IsMainBranch" = TRUE
                      AND "IsActive" = TRUE
                      AND "DeletedAt" IS NULL
                )
                UPDATE "Orders" target
                SET "BranchId" = main_branches."Id"
                FROM main_branches
                WHERE target."BranchId" IS NULL
                  AND target."TenantId" = main_branches."TenantId";

                UPDATE "OrderItems" target
                SET "BranchId" = parent."BranchId"
                FROM "Orders" parent
                WHERE target."BranchId" IS NULL
                  AND target."OrderId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                WITH main_branches AS (
                    SELECT "TenantId", "Id"
                    FROM "Branches"
                    WHERE "IsMainBranch" = TRUE
                      AND "IsActive" = TRUE
                      AND "DeletedAt" IS NULL
                )
                UPDATE "OrderItems" target
                SET "BranchId" = main_branches."Id"
                FROM main_branches
                WHERE target."BranchId" IS NULL
                  AND target."TenantId" = main_branches."TenantId";

                UPDATE "Payments" target
                SET "BranchId" = parent."BranchId"
                FROM "Orders" parent
                WHERE target."BranchId" IS NULL
                  AND target."OrderId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "RefundLogs" target
                SET "BranchId" = parent."BranchId"
                FROM "Orders" parent
                WHERE target."BranchId" IS NULL
                  AND target."OrderId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "CancelLogs" target
                SET "BranchId" = parent."BranchId"
                FROM "Orders" parent
                WHERE target."BranchId" IS NULL
                  AND target."OrderId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "PrintJobs" target
                SET "BranchId" = parent."BranchId"
                FROM "Orders" parent
                WHERE target."BranchId" IS NULL
                  AND target."OrderId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "PaymentAdjustments" target
                SET "BranchId" = parent."BranchId"
                FROM "Orders" parent
                WHERE target."BranchId" IS NULL
                  AND target."OrderId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "PaymentAdjustmentDetails" target
                SET "BranchId" = parent."BranchId"
                FROM "PaymentAdjustments" parent
                WHERE target."BranchId" IS NULL
                  AND target."AdjustmentId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "PaymentAdjustmentDetails" target
                SET "BranchId" = parent."BranchId"
                FROM "Payments" parent
                WHERE target."BranchId" IS NULL
                  AND target."PaymentId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "WasteLogs" target
                SET "BranchId" = parent."BranchId"
                FROM "Orders" parent
                WHERE target."BranchId" IS NULL
                  AND (target."OrderId" = parent."Id" OR target."SourceOrderId" = parent."Id")
                  AND parent."BranchId" IS NOT NULL;

                WITH main_branches AS (
                    SELECT "TenantId", "Id"
                    FROM "Branches"
                    WHERE "IsMainBranch" = TRUE
                      AND "IsActive" = TRUE
                      AND "DeletedAt" IS NULL
                )
                UPDATE "PurchaseOrders" target
                SET "BranchId" = main_branches."Id"
                FROM main_branches
                WHERE target."BranchId" IS NULL
                  AND target."TenantId" = main_branches."TenantId";

                UPDATE "PurchaseOrderItems" target
                SET "BranchId" = parent."BranchId"
                FROM "PurchaseOrders" parent
                WHERE target."BranchId" IS NULL
                  AND target."PurchaseOrderId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "StockBatches" target
                SET "BranchId" = parent."BranchId"
                FROM "PurchaseOrders" parent
                WHERE target."BranchId" IS NULL
                  AND target."PurchaseOrderId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                UPDATE "InventoryTransactions" target
                SET "BranchId" = parent."BranchId"
                FROM "WasteLogs" parent
                WHERE target."BranchId" IS NULL
                  AND target."WasteLogId" = parent."Id"
                  AND parent."BranchId" IS NOT NULL;

                WITH main_branches AS (
                    SELECT "TenantId", "Id"
                    FROM "Branches"
                    WHERE "IsMainBranch" = TRUE
                      AND "IsActive" = TRUE
                      AND "DeletedAt" IS NULL
                )
                UPDATE "Payments" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "RefundLogs" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "CancelLogs" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "PrintJobs" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "PaymentAdjustments" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "PaymentAdjustmentDetails" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "WasteLogs" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "PurchaseOrderItems" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "StockBatches" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "RawMaterialInventories" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "InventoryTransfers" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "InventoryTransactions" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "ExpenseInvoices" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "CashierBalanceShifts" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";
                WITH main_branches AS (
                    SELECT "TenantId", "Id" FROM "Branches" WHERE "IsMainBranch" = TRUE AND "IsActive" = TRUE AND "DeletedAt" IS NULL
                )
                UPDATE "Notifications" target SET "BranchId" = main_branches."Id" FROM main_branches WHERE target."BranchId" IS NULL AND target."TenantId" = main_branches."TenantId";

                DO $$
                DECLARE missing_tables text;
                BEGIN
                    SELECT string_agg("TableName", ', ' ORDER BY "TableName")
                    INTO missing_tables
                    FROM (
                        SELECT 'CancelLogs' AS "TableName" WHERE EXISTS (SELECT 1 FROM "CancelLogs" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'CashierBalanceShifts' WHERE EXISTS (SELECT 1 FROM "CashierBalanceShifts" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'ExpenseInvoices' WHERE EXISTS (SELECT 1 FROM "ExpenseInvoices" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'InventoryTransactions' WHERE EXISTS (SELECT 1 FROM "InventoryTransactions" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'InventoryTransfers' WHERE EXISTS (SELECT 1 FROM "InventoryTransfers" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'Notifications' WHERE EXISTS (SELECT 1 FROM "Notifications" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'OrderItems' WHERE EXISTS (SELECT 1 FROM "OrderItems" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'Orders' WHERE EXISTS (SELECT 1 FROM "Orders" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'PaymentAdjustmentDetails' WHERE EXISTS (SELECT 1 FROM "PaymentAdjustmentDetails" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'PaymentAdjustments' WHERE EXISTS (SELECT 1 FROM "PaymentAdjustments" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'Payments' WHERE EXISTS (SELECT 1 FROM "Payments" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'PrintJobs' WHERE EXISTS (SELECT 1 FROM "PrintJobs" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'PurchaseOrderItems' WHERE EXISTS (SELECT 1 FROM "PurchaseOrderItems" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'PurchaseOrders' WHERE EXISTS (SELECT 1 FROM "PurchaseOrders" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'RawMaterialInventories' WHERE EXISTS (SELECT 1 FROM "RawMaterialInventories" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'RefundLogs' WHERE EXISTS (SELECT 1 FROM "RefundLogs" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'StockBatches' WHERE EXISTS (SELECT 1 FROM "StockBatches" WHERE "BranchId" IS NULL)
                        UNION ALL SELECT 'WasteLogs' WHERE EXISTS (SELECT 1 FROM "WasteLogs" WHERE "BranchId" IS NULL)
                    ) failed;

                    IF missing_tables IS NOT NULL THEN
                        RAISE EXCEPTION 'Operational BranchId backfill failed for tables: %', missing_tables;
                    END IF;
                END $$;

                ALTER TABLE "CancelLogs" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "CashierBalanceShifts" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "ExpenseInvoices" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "InventoryTransactions" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "InventoryTransfers" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "Notifications" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "OrderItems" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "Orders" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "PaymentAdjustmentDetails" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "PaymentAdjustments" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "Payments" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "PrintJobs" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "PurchaseOrderItems" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "PurchaseOrders" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "RawMaterialInventories" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "RefundLogs" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "StockBatches" ALTER COLUMN "BranchId" SET NOT NULL;
                ALTER TABLE "WasteLogs" ALTER COLUMN "BranchId" SET NOT NULL;
                """);

            CreateIndexes(migrationBuilder);
            AddBranchForeignKeys(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropBranchForeignKeys(migrationBuilder);
            DropIndexes(migrationBuilder);

            foreach (var table in NewBranchColumnTables)
            {
                migrationBuilder.DropColumn(
                    name: "BranchId",
                    table: table);
            }

            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                table: "WasteLogs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                table: "ExpenseInvoices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialInventories_Tenant_Material_Warehouse",
                table: "RawMaterialInventories",
                columns: new[] { "TenantId", "RawMaterialId", "WarehouseId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialInventories_Tenant_Warehouse",
                table: "RawMaterialInventories",
                columns: new[] { "TenantId", "WarehouseId" });
        }

        private static void CreateIndexes(MigrationBuilder migrationBuilder)
        {
            foreach (var index in Indexes)
            {
                migrationBuilder.CreateIndex(
                    name: index.Name,
                    table: index.Table,
                    columns: index.Columns,
                    unique: index.Unique,
                    filter: index.Filter);
            }
        }

        private static void DropIndexes(MigrationBuilder migrationBuilder)
        {
            foreach (var index in Indexes)
            {
                migrationBuilder.DropIndex(
                    name: index.Name,
                    table: index.Table);
            }
        }

        private static void AddBranchForeignKeys(MigrationBuilder migrationBuilder)
        {
            foreach (var foreignKey in BranchForeignKeys)
            {
                migrationBuilder.AddForeignKey(
                    name: foreignKey.Name,
                    table: foreignKey.Table,
                    column: "BranchId",
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            }
        }

        private static void DropBranchForeignKeys(MigrationBuilder migrationBuilder)
        {
            foreach (var foreignKey in BranchForeignKeys)
            {
                migrationBuilder.DropForeignKey(
                    name: foreignKey.Name,
                    table: foreignKey.Table);
            }
        }
    }
}
