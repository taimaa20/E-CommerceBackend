using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Modules.Retail.Domain;

namespace RestaurantPos.Api.Modules.Retail.Data
{
    /// <summary>
    /// Single EF configuration entry point for the Retail module, mirroring
    /// <c>ApplyMarketingEngineConfigurations</c>. Called once from
    /// <c>PosDbContext.OnModelCreating</c>.
    /// </summary>
    public static class RetailModelConfiguration
    {
        public static ModelBuilder ApplyRetailConfigurations(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RetailProductDetail>(b =>
            {
                b.HasOne(d => d.Product)
                    .WithMany()
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(d => d.Supplier)
                    .WithMany()
                    .HasForeignKey(d => d.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(d => d.ProductId)
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_RetailProductDetails_Product");

                b.HasIndex(d => new { d.TenantId, d.Sku })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_RetailProductDetails_Tenant_Sku");

                b.HasIndex(d => new { d.TenantId, d.Barcode })
                    .HasDatabaseName("IX_RetailProductDetails_Tenant_Barcode");

                b.HasIndex(d => new { d.TenantId, d.SupplierId })
                    .HasDatabaseName("IX_RetailProductDetails_Tenant_Supplier");
            });

            modelBuilder.Entity<RetailLegacyPurchase>(b =>
            {
                b.HasOne(p => p.Supplier)
                    .WithMany()
                    .HasForeignKey(p => p.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(p => new { p.TenantId, p.PurchaseOrderNumber })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_RetailLegacyPurchases_Tenant_Number");

                b.HasIndex(p => new { p.TenantId, p.BranchId, p.OrderDateUtc })
                    .HasDatabaseName("IX_RetailLegacyPurchases_Tenant_Branch_Date");
            });

            modelBuilder.Entity<RetailStockMovement>(b =>
            {
                b.HasOne(m => m.Product)
                    .WithMany()
                    .HasForeignKey(m => m.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Stock on hand is a running SUM over this index.
                b.HasIndex(m => new { m.TenantId, m.BranchId, m.ProductId })
                    .HasDatabaseName("IX_RetailStockMovements_Tenant_Branch_Product");

                b.HasIndex(m => new { m.TenantId, m.OccurredAtUtc })
                    .HasDatabaseName("IX_RetailStockMovements_Tenant_OccurredAt");

                b.HasIndex(m => new { m.TenantId, m.SourceDocumentType, m.SourceDocumentId })
                    .HasDatabaseName("IX_RetailStockMovements_Tenant_SourceDocument");

                // Exactly-once, enforced by the database rather than by application ordering:
                // one movement of a given type per source line. A replayed sale, a duplicated
                // receipt or a re-fired reversal violates this index instead of doubling stock.
                b.HasIndex(m => new { m.TenantId, m.MovementType, m.SourceLineId })
                    .IsUnique()
                    .HasFilter("\"SourceLineId\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_RetailStockMovements_Line_Once");

                // The opening balance is a singleton per product and branch, so re-running the
                // ledger initialisation can never add a second one.
                b.HasIndex(m => new { m.TenantId, m.BranchId, m.ProductId, m.MovementType })
                    .IsUnique()
                    .HasFilter("\"MovementType\" = 0 AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_RetailStockMovements_Opening_Once");
            });

            modelBuilder.Entity<RetailPurchaseOrder>(b =>
            {
                b.HasOne(p => p.Supplier)
                    .WithMany()
                    .HasForeignKey(p => p.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasMany(p => p.Lines)
                    .WithOne(l => l.RetailPurchaseOrder!)
                    .HasForeignKey(l => l.RetailPurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(p => new { p.TenantId, p.OrderNumber })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_RetailPurchaseOrders_Tenant_Number");

                b.HasIndex(p => new { p.TenantId, p.BranchId, p.Status, p.OrderDateUtc })
                    .HasDatabaseName("IX_RetailPurchaseOrders_Tenant_Branch_Status_Date");

                b.HasIndex(p => new { p.TenantId, p.SupplierId })
                    .HasDatabaseName("IX_RetailPurchaseOrders_Tenant_Supplier");
            });

            modelBuilder.Entity<RetailPurchaseOrderLine>(b =>
            {
                b.HasOne(l => l.Product)
                    .WithMany()
                    .HasForeignKey(l => l.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(l => new { l.TenantId, l.RetailPurchaseOrderId })
                    .HasDatabaseName("IX_RetailPurchaseOrderLines_Tenant_Order");

                b.HasIndex(l => new { l.TenantId, l.ProductId })
                    .HasDatabaseName("IX_RetailPurchaseOrderLines_Tenant_Product");
            });

            return modelBuilder;
        }
    }
}
