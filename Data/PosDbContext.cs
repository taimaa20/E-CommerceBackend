using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Data;
using RestaurantPos.Api.Modules.Payments.Data;
using RestaurantPos.Api.Modules.Retail.Data;
using RestaurantPos.Api.Services;
using System.Linq.Expressions;

namespace RestaurantPos.Api.Data
{
    public class PosDbContext : DbContext
    {
        private readonly ITenantResolver _tenantResolver;

        public PosDbContext(DbContextOptions<PosDbContext> options, ITenantResolver tenantResolver) : base(options)
        {
            _tenantResolver = tenantResolver;
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Subcategory> Subcategories { get; set; }
        public DbSet<ModifierGroup> ModifierGroups { get; set; }
        public DbSet<Modifier> Modifiers { get; set; }
        public DbSet<ModifierRecipeItem> ModifierRecipeItems { get; set; }
        public DbSet<ProductModifierGroup> ProductModifierGroups { get; set; }
        public DbSet<ProductOption> ProductOptions { get; set; }
        public DbSet<ProductOptionRecipeItem> ProductOptionRecipeItems { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<UserBranch> UserBranches { get; set; }
        public DbSet<BranchProduct> BranchProducts { get; set; }
        public DbSet<BranchCategory> BranchCategories { get; set; }
        public DbSet<BranchSubcategory> BranchSubcategories { get; set; }
        public DbSet<BranchModifier> BranchModifiers { get; set; }
        public DbSet<BranchModifierGroup> BranchModifierGroups { get; set; }
        public DbSet<BranchProductOption> BranchProductOptions { get; set; }
        public DbSet<BranchPaymentMethod> BranchPaymentMethods { get; set; }
        public DbSet<BranchDeliveryPartner> BranchDeliveryPartners { get; set; }
        public DbSet<BranchPrinter> BranchPrinters { get; set; }
        public DbSet<BranchOffer> BranchOffers { get; set; }
        public DbSet<BranchSettings> BranchSettings { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<PublicReceiptToken> PublicReceiptTokens { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<PaymentAdjustment> PaymentAdjustments { get; set; }
        public DbSet<PaymentAdjustmentDetail> PaymentAdjustmentDetails { get; set; }
        public DbSet<VoucherUsageAudit> VoucherUsageAudits { get; set; }
        public DbSet<PartnerPriceOverrideAudit> PartnerPriceOverrideAudits { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderItemModifier> OrderItemModifiers { get; set; }
        public DbSet<OrderItemRecipeSnapshot> OrderItemRecipeSnapshots { get; set; }
        public DbSet<OrderDisplaySequence> OrderDisplaySequences { get; set; }
        public DbSet<OrderNumberingConfig> OrderNumberingConfigs { get; set; }
        public DbSet<ShiftRulesConfig> ShiftRulesConfigs { get; set; }
        public DbSet<ConfigAuditLog> ConfigAuditLogs { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<TableCategory> TableCategories { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<DiscountGroup> DiscountGroups { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<CashierBalanceShift> CashierBalanceShifts { get; set; }
        
        // HR System
        public DbSet<StaffProfile> StaffProfiles { get; set; }
        public DbSet<TimeEntry> TimeEntries { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<MobileLeaveRequest> MobileLeaveRequests { get; set; }
        public DbSet<MobileLoanRequest> MobileLoanRequests { get; set; }
        public DbSet<MobilePermissionRequest> MobilePermissionRequests { get; set; }
        public DbSet<Bonus> Bonuses { get; set; }

        // Inventory
        public DbSet<RawMaterial> RawMaterials { get; set; }
        public DbSet<RecipeItem> RecipeItems { get; set; }
        public DbSet<RecipeItemAlternative> RecipeItemAlternatives { get; set; }
        public DbSet<Offer> Offers { get; set; }
        public DbSet<OfferProduct> OfferProducts { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<StockBatch> StockBatches { get; set; }
        public DbSet<StockAdjustmentLog> StockAdjustmentLogs { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }

        // Notifications
        public DbSet<Notification> Notifications { get; set; }

        // Settings
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<FollowUsClick> FollowUsClicks { get; set; }

        // Waste & Cancel Log
        public DbSet<WasteLog> WasteLogs { get; set; }
        public DbSet<WasteLogAudit> WasteLogAudits { get; set; }
        public DbSet<WasteLogEmployee> WasteLogEmployees { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<CancelLog> CancelLogs { get; set; }
        public DbSet<CancelReason> CancelReasons { get; set; }
        public DbSet<RefundLog> RefundLogs { get; set; }

        // Menu & QR Code Management
        public DbSet<TenantMenuSettings> TenantMenuSettings { get; set; }
        public DbSet<QrCodeAccess> QrCodeAccess { get; set; }
        public DbSet<MenuAccessLog> MenuAccessLogs { get; set; }

        // Multi-kitchen routing & direct printing
        public DbSet<Kitchen> Kitchens { get; set; }
        public DbSet<Printer> Printers { get; set; }
        public DbSet<PrintJob> PrintJobs { get; set; }
        public DbSet<ProductKitchenPrinter> ProductKitchenPrinters { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<StorefrontBanner> StorefrontBanners { get; set; }
        public DbSet<ProductBrand> ProductBrands { get; set; }

        // Small configuration lookups. Neither is referenced by a foreign key: Currencies says
        // which codes may be OFFERED for selection (money itself stays a plain decimal plus the
        // code on SystemSettings), and WhatsAppContacts holds the storefront's contact
        // destinations and their message templates.
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<WhatsAppContact> WhatsAppContacts { get; set; }

        // Multi-warehouse inventory
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<RawMaterialInventory> RawMaterialInventories { get; set; }
        public DbSet<InventoryTransfer> InventoryTransfers { get; set; }

        // Durable replay protection for offline-first client mutations.
        public DbSet<IdempotencyEntry> IdempotencyEntries { get; set; }

        // Expense / supplier / store-expense invoices (lightweight ERP module).
        public DbSet<ExpenseInvoice> ExpenseInvoices { get; set; }
        public DbSet<ExpenseInvoiceAttachment> ExpenseInvoiceAttachments { get; set; }
        public DbSet<ExpenseInvoiceAuditLog> ExpenseInvoiceAuditLogs { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }

        // Enterprise asset management.
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetCategory> AssetCategories { get; set; }
        public DbSet<AssetAttachment> AssetAttachments { get; set; }
        public DbSet<AssetMaintenanceRecord> AssetMaintenanceRecords { get; set; }
        public DbSet<AssetActivityLog> AssetActivityLogs { get; set; }

        // Delivery management (zones + order fee/cost snapshots).
        public DbSet<DeliveryZone> DeliveryZones { get; set; }
        public DbSet<DeliveryPartner> DeliveryPartners { get; set; }
        public DbSet<DeliveryPartnerProduct> DeliveryPartnerProducts { get; set; }
        public DbSet<DeliveryPartnerActivityLog> DeliveryPartnerActivityLogs { get; set; }
        public DbSet<CostSharingProvider> CostSharingProviders { get; set; }
        public DbSet<CostSharingRule> CostSharingRules { get; set; }
        public DbSet<CostSharingOverrideAudit> CostSharingOverrideAudits { get; set; }
        public DbSet<OrderProfitabilitySnapshot> OrderProfitabilitySnapshots { get; set; }
        public DbSet<OrderProfitabilitySnapshotItem> OrderProfitabilitySnapshotItems { get; set; }

        // Customer mobile module.
        public DbSet<CustomerAccount> CustomerAccounts { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<CustomerDevice> CustomerDevices { get; set; }
        public DbSet<CustomerRefreshToken> CustomerRefreshTokens { get; set; }
        public DbSet<CustomerCart> CustomerCarts { get; set; }
        public DbSet<CustomerCartItem> CustomerCartItems { get; set; }
        public DbSet<CustomerCartItemModifier> CustomerCartItemModifiers { get; set; }
        public DbSet<CustomerMobileOrder> CustomerMobileOrders { get; set; }
        public DbSet<CustomerMobileAuditLog> CustomerMobileAuditLogs { get; set; }
        public DbSet<CustomerOtp> CustomerOtps { get; set; }

        // Marketing Engine module (isolated under Modules/Marketing).
        public DbSet<Modules.Marketing.Domain.CustomerWallet> CustomerWallets { get; set; }
        public DbSet<Modules.Marketing.Domain.WalletTransaction> WalletTransactions { get; set; }
        public DbSet<Modules.Marketing.Domain.PointsLot> PointsLots { get; set; }
        public DbSet<Modules.Marketing.Domain.EarningRule> EarningRules { get; set; }
        public DbSet<Modules.Marketing.Domain.MarketingSettings> MarketingSettings { get; set; }
        public DbSet<Modules.Marketing.Domain.CustomerSegment> CustomerSegments { get; set; }
        public DbSet<Modules.Marketing.Domain.SegmentRule> SegmentRules { get; set; }
        public DbSet<Modules.Marketing.Domain.SegmentMember> SegmentMembers { get; set; }
        public DbSet<Modules.Marketing.Domain.LoyaltyTier> LoyaltyTiers { get; set; }
        public DbSet<Modules.Marketing.Domain.TierBenefit> TierBenefits { get; set; }
        public DbSet<Modules.Marketing.Domain.CustomerTierHistory> CustomerTierHistories { get; set; }
        public DbSet<Modules.Marketing.Domain.MarketingAuditLog> MarketingAuditLogs { get; set; }
        public DbSet<Modules.Marketing.Domain.CustomerMergeHistory> CustomerMergeHistories { get; set; }
        public DbSet<Modules.Marketing.Domain.Campaign> Campaigns { get; set; }
        public DbSet<Modules.Marketing.Domain.CampaignCustomer> CampaignCustomers { get; set; }
        public DbSet<Modules.Marketing.Domain.Reward> Rewards { get; set; }
        public DbSet<Modules.Marketing.Domain.RewardRedemption> RewardRedemptions { get; set; }

        // Payment Engine module (provider-agnostic foundation).
        public DbSet<Modules.Payments.Domain.Entities.Payment> PaymentEnginePayments { get; set; }
        public DbSet<Modules.Payments.Domain.Entities.PaymentAttempt> PaymentEnginePaymentAttempts { get; set; }
        public DbSet<Modules.Payments.Domain.Entities.PaymentSession> PaymentEnginePaymentSessions { get; set; }
        public DbSet<Modules.Payments.Domain.Entities.PaymentEvent> PaymentEnginePaymentEvents { get; set; }

        // Retail module. Retail-only attributes of a Product; absence of a row means the
        // product is an ordinary restaurant product and nothing here applies to it.
        public DbSet<Modules.Retail.Domain.RetailProductDetail> RetailProductDetails { get; set; }
        public DbSet<Modules.Retail.Domain.RetailLegacyPurchase> RetailLegacyPurchases { get; set; }

        // Append-only finished-goods ledger. Retail stock on hand is SUM(Quantity) over it —
        // there is no stored balance anywhere else.
        public DbSet<Modules.Retail.Domain.RetailStockMovement> RetailStockMovements { get; set; }

        // Operational retail purchasing with SKU-level lines, on the shared Supplier master.
        public DbSet<Modules.Retail.Domain.RetailPurchaseOrder> RetailPurchaseOrders { get; set; }
        public DbSet<Modules.Retail.Domain.RetailPurchaseOrderLine> RetailPurchaseOrderLines { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global Query Filter for Multi-tenancy + Soft Delete
            // Combined filter: e.TenantId == currentTenant && e.DeletedAt == null
            // To bypass either filter, use IgnoreQueryFilters() on the specific query.
            Expression<Func<Guid>> tenantIdAccessor = () => _tenantResolver.GetTenantId();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var param = Expression.Parameter(entityType.ClrType, "e");

                    // e.TenantId == _tenantResolver.GetTenantId()
                    var tenantProp = Expression.Property(param, nameof(BaseEntity.TenantId));
                    var tenantFilter = Expression.Equal(tenantProp, Expression.Invoke(tenantIdAccessor));

                    // e.DeletedAt == null
                    var deletedProp = Expression.Property(param, nameof(BaseEntity.DeletedAt));
                    var notDeleted = Expression.Equal(deletedProp, Expression.Constant(null, typeof(DateTime?)));

                    var combined = Expression.AndAlso(tenantFilter, notDeleted);
                    var filter = Expression.Lambda(combined, param);

                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
                }
            }

            modelBuilder.Entity<Branch>(b =>
            {
                b.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_Branches_Name_Normalized",
                        "\"Name\" = btrim(\"Name\") AND length(\"Name\") > 0");
                    t.HasCheckConstraint(
                        "CK_Branches_Code_Normalized",
                        "\"Code\" = upper(replace(btrim(\"Code\"), ' ', '_')) AND length(\"Code\") > 0");
                });

                b.HasIndex(x => new { x.TenantId, x.Code })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_Branches_Tenant_Code");

                b.HasIndex(x => new { x.TenantId, x.Name })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_Branches_Tenant_Name");

                b.HasIndex(x => new { x.TenantId, x.IsMainBranch })
                    .IsUnique()
                    .HasFilter("\"IsMainBranch\" = TRUE AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_Branches_Tenant_MainBranch");

                b.HasIndex(x => new { x.TenantId, x.IsActive, x.Name })
                    .HasDatabaseName("IX_Branches_Tenant_Active_Name");
            });

            modelBuilder.Entity<UserBranch>(b =>
            {
                b.HasOne(ub => ub.User)
                    .WithMany()
                    .HasForeignKey(ub => ub.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(ub => ub.Branch)
                    .WithMany()
                    .HasForeignKey(ub => ub.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(ub => new { ub.TenantId, ub.UserId, ub.BranchId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_UserBranches_Tenant_User_Branch");

                b.HasIndex(ub => new { ub.TenantId, ub.UserId, ub.IsDefault })
                    .IsUnique()
                    .HasFilter("\"IsDefault\" = TRUE AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_UserBranches_Tenant_User_Default");

                b.HasIndex(ub => new { ub.TenantId, ub.BranchId })
                    .HasDatabaseName("IX_UserBranches_Tenant_Branch");
            });

            modelBuilder.Entity<BranchProduct>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.Product)
                    .WithMany()
                    .HasForeignKey(c => c.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.ProductId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchProducts_Tenant_Branch_Product");

                b.HasIndex(c => new { c.TenantId, c.ProductId })
                    .HasDatabaseName("IX_BranchProducts_Tenant_Product");

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.DisplayOrder })
                    .HasDatabaseName("IX_BranchProducts_Tenant_Branch_Order");
            });

            modelBuilder.Entity<StockAdjustmentLog>(b =>
            {
                b.HasOne(a => a.RawMaterial)
                    .WithMany()
                    .HasForeignKey(a => a.MaterialId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(a => a.Branch)
                    .WithMany()
                    .HasForeignKey(a => a.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(a => a.PerformedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.PerformedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(a => new { a.TenantId, a.BranchId, a.MaterialId, a.CreatedAt })
                    .HasDatabaseName("IX_StockAdjustmentLogs_Tenant_Branch_Material_Created");
            });

            modelBuilder.Entity<BranchCategory>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.Category)
                    .WithMany()
                    .HasForeignKey(c => c.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.CategoryId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchCategories_Tenant_Branch_Category");

                b.HasIndex(c => new { c.TenantId, c.CategoryId })
                    .HasDatabaseName("IX_BranchCategories_Tenant_Category");

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.DisplayOrder })
                    .HasDatabaseName("IX_BranchCategories_Tenant_Branch_Order");
            });

            modelBuilder.Entity<BranchSubcategory>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.Subcategory)
                    .WithMany()
                    .HasForeignKey(c => c.SubcategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.SubcategoryId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchSubcategories_Tenant_Branch_Subcategory");

                b.HasIndex(c => new { c.TenantId, c.SubcategoryId })
                    .HasDatabaseName("IX_BranchSubcategories_Tenant_Subcategory");

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.DisplayOrder })
                    .HasDatabaseName("IX_BranchSubcategories_Tenant_Branch_Order");
            });

            modelBuilder.Entity<BranchModifier>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.Modifier)
                    .WithMany()
                    .HasForeignKey(c => c.ModifierId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.ModifierId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchModifiers_Tenant_Branch_Modifier");

                b.HasIndex(c => new { c.TenantId, c.ModifierId })
                    .HasDatabaseName("IX_BranchModifiers_Tenant_Modifier");
            });

            modelBuilder.Entity<BranchModifierGroup>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.ModifierGroup)
                    .WithMany()
                    .HasForeignKey(c => c.ModifierGroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.ModifierGroupId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchModifierGroups_Tenant_Branch_Group");

                b.HasIndex(c => new { c.TenantId, c.ModifierGroupId })
                    .HasDatabaseName("IX_BranchModifierGroups_Tenant_Group");

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.DisplayOrder })
                    .HasDatabaseName("IX_BranchModifierGroups_Tenant_Branch_Order");
            });

            modelBuilder.Entity<BranchProductOption>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.ProductOption)
                    .WithMany()
                    .HasForeignKey(c => c.ProductOptionId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.ProductOptionId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchProductOptions_Tenant_Branch_Option");

                b.HasIndex(c => new { c.TenantId, c.ProductOptionId })
                    .HasDatabaseName("IX_BranchProductOptions_Tenant_Option");

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.DisplayOrder })
                    .HasDatabaseName("IX_BranchProductOptions_Tenant_Branch_Order");
            });

            modelBuilder.Entity<BranchPaymentMethod>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.PaymentMethod)
                    .WithMany()
                    .HasForeignKey(c => c.PaymentMethodId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.PaymentMethodId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchPaymentMethods_Tenant_Branch_Method");

                b.HasIndex(c => new { c.TenantId, c.PaymentMethodId })
                    .HasDatabaseName("IX_BranchPaymentMethods_Tenant_Method");
            });

            modelBuilder.Entity<BranchDeliveryPartner>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.DeliveryPartner)
                    .WithMany()
                    .HasForeignKey(c => c.DeliveryPartnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.DeliveryPartnerId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchDeliveryPartners_Tenant_Branch_Partner");

                b.HasIndex(c => new { c.TenantId, c.DeliveryPartnerId })
                    .HasDatabaseName("IX_BranchDeliveryPartners_Tenant_Partner");
            });

            modelBuilder.Entity<BranchPrinter>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.Printer)
                    .WithMany()
                    .HasForeignKey(c => c.PrinterId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.PrinterId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchPrinters_Tenant_Branch_Printer");

                b.HasIndex(c => new { c.TenantId, c.PrinterId })
                    .HasDatabaseName("IX_BranchPrinters_Tenant_Printer");

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.DisplayOrder })
                    .HasDatabaseName("IX_BranchPrinters_Tenant_Branch_Order");
            });

            modelBuilder.Entity<BranchOffer>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(c => c.Offer)
                    .WithMany()
                    .HasForeignKey(c => c.OfferId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.OfferId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchOffers_Tenant_Branch_Offer");

                b.HasIndex(c => new { c.TenantId, c.OfferId })
                    .HasDatabaseName("IX_BranchOffers_Tenant_Offer");
            });

            modelBuilder.Entity<BranchSettings>(b =>
            {
                b.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_BranchSettings_Key_NotEmpty",
                        "length(btrim(\"SettingKey\")) > 0");
                });

                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.SettingKey })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_BranchSettings_Tenant_Branch_Key");

                b.HasIndex(c => new { c.TenantId, c.BranchId })
                    .HasDatabaseName("IX_BranchSettings_Tenant_Branch");
            });

            modelBuilder.Entity<Order>(b =>
            {
                b.HasOne(o => o.Branch)
                    .WithMany()
                    .HasForeignKey(o => o.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(o => new { o.TenantId, o.BranchId, o.CreatedAt })
                    .HasDatabaseName("IX_Orders_Tenant_Branch_CreatedAt");

                b.HasIndex(o => new { o.TenantId, o.BranchId, o.Status, o.CreatedAt })
                    .HasDatabaseName("IX_Orders_Tenant_Branch_Status_CreatedAt");

                b.HasIndex(o => new { o.TenantId, o.BranchId, o.OrderType, o.Status, o.CreatedAt })
                    .HasDatabaseName("IX_Orders_Tenant_Branch_Type_Status_CreatedAt");
            });

            modelBuilder.Entity<OrderItem>(b =>
            {
                b.HasOne(i => i.Branch)
                    .WithMany()
                    .HasForeignKey(i => i.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(i => new { i.TenantId, i.BranchId, i.OrderId })
                    .HasDatabaseName("IX_OrderItems_Tenant_Branch_Order");

                b.HasIndex(i => new { i.TenantId, i.BranchId, i.ProductId, i.CreatedAt })
                    .HasDatabaseName("IX_OrderItems_Tenant_Branch_Product_CreatedAt");
            });

            modelBuilder.Entity<Payment>(b =>
            {
                b.HasOne(p => p.Branch)
                    .WithMany()
                    .HasForeignKey(p => p.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(p => new { p.TenantId, p.BranchId, p.CreatedAt })
                    .HasDatabaseName("IX_Payments_Tenant_Branch_CreatedAt");

                b.HasIndex(p => new { p.TenantId, p.BranchId, p.OrderId, p.CreatedAt })
                    .HasDatabaseName("IX_Payments_Tenant_Branch_Order_CreatedAt");
            });

            modelBuilder.Entity<PaymentAdjustment>(b =>
            {
                b.HasOne(a => a.Branch)
                    .WithMany()
                    .HasForeignKey(a => a.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(a => new { a.TenantId, a.BranchId, a.OrderId, a.Status })
                    .HasDatabaseName("IX_PaymentAdjustments_Tenant_Branch_Order_Status");
            });

            modelBuilder.Entity<PaymentAdjustmentDetail>(b =>
            {
                b.HasOne(d => d.Branch)
                    .WithMany()
                    .HasForeignKey(d => d.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(d => new { d.TenantId, d.BranchId, d.AdjustmentId })
                    .HasDatabaseName("IX_PaymentAdjustmentDetails_Tenant_Branch_Adjustment");
            });

            modelBuilder.Entity<PurchaseOrder>(b =>
            {
                b.HasOne(o => o.Branch)
                    .WithMany()
                    .HasForeignKey(o => o.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(o => new { o.TenantId, o.BranchId, o.Status, o.ExpectedDate })
                    .HasDatabaseName("IX_PurchaseOrders_Tenant_Branch_Status_Date");

                b.HasIndex(o => new { o.TenantId, o.BranchId, o.SupplierId })
                    .HasDatabaseName("IX_PurchaseOrders_Tenant_Branch_Supplier");
            });

            modelBuilder.Entity<PurchaseOrderItem>(b =>
            {
                b.HasOne(i => i.Branch)
                    .WithMany()
                    .HasForeignKey(i => i.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(i => new { i.TenantId, i.BranchId, i.PurchaseOrderId })
                    .HasDatabaseName("IX_PurchaseOrderItems_Tenant_Branch_Order");
            });

            modelBuilder.Entity<StockBatch>(b =>
            {
                b.HasOne(s => s.Branch)
                    .WithMany()
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(s => new { s.TenantId, s.BranchId, s.MaterialId, s.Status, s.ExpiryDate })
                    .HasDatabaseName("IX_StockBatches_Tenant_Branch_Material_Status_Expiry");
            });

            modelBuilder.Entity<CashierBalanceShift>(b =>
            {
                b.HasOne(s => s.Branch)
                    .WithMany()
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(s => new { s.TenantId, s.BranchId, s.CashierId, s.OpenedAt })
                    .HasDatabaseName("IX_CashierBalanceShifts_Tenant_Branch_Cashier_OpenedAt");
            });

            modelBuilder.Entity<Notification>(b =>
            {
                b.HasOne(n => n.Branch)
                    .WithMany()
                    .HasForeignKey(n => n.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(n => new { n.TenantId, n.BranchId, n.IsRead, n.CreatedAt })
                    .HasDatabaseName("IX_Notifications_Tenant_Branch_IsRead_CreatedAt");
            });

            modelBuilder.Entity<WasteLog>(b =>
            {
                b.HasOne(w => w.Branch)
                    .WithMany()
                    .HasForeignKey(w => w.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(w => new { w.TenantId, w.BranchId, w.CreatedAt })
                    .HasDatabaseName("IX_WasteLogs_Tenant_Branch_CreatedAt");

                b.HasIndex(w => new { w.TenantId, w.BranchId, w.Status, w.CreatedAt })
                    .HasDatabaseName("IX_WasteLogs_Tenant_Branch_Status_CreatedAt");
            });

            modelBuilder.Entity<InventoryTransaction>(b =>
            {
                b.HasOne(t => t.Branch)
                    .WithMany()
                    .HasForeignKey(t => t.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(t => new { t.TenantId, t.BranchId, t.TransactionType, t.CreatedAt })
                    .HasDatabaseName("IX_InventoryTransactions_Tenant_Branch_Type_CreatedAt");
            });

            modelBuilder.Entity<CancelLog>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.CancelledAt })
                    .HasDatabaseName("IX_CancelLogs_Tenant_Branch_CancelledAt");
            });

            modelBuilder.Entity<RefundLog>(b =>
            {
                b.HasOne(r => r.Branch)
                    .WithMany()
                    .HasForeignKey(r => r.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(r => new { r.TenantId, r.BranchId, r.ProcessedAt })
                    .HasDatabaseName("IX_RefundLogs_Tenant_Branch_ProcessedAt");
            });

            modelBuilder.Entity<PrintJob>(b =>
            {
                b.HasOne(j => j.Branch)
                    .WithMany()
                    .HasForeignKey(j => j.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(j => new { j.TenantId, j.BranchId, j.Status, j.NextAttemptAt })
                    .HasDatabaseName("IX_PrintJobs_Tenant_Branch_Status_Next");
            });

            modelBuilder.Entity<RawMaterialInventory>(b =>
            {
                b.HasOne(i => i.Branch)
                    .WithMany()
                    .HasForeignKey(i => i.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(i => new { i.TenantId, i.BranchId, i.RawMaterialId, i.WarehouseId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_RawMaterialInventories_Tenant_Branch_Material_Warehouse");
            });

            modelBuilder.Entity<InventoryTransfer>(b =>
            {
                b.HasOne(t => t.Branch)
                    .WithMany()
                    .HasForeignKey(t => t.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(t => new { t.TenantId, t.BranchId, t.CreatedAt })
                    .HasDatabaseName("IX_InventoryTransfers_Tenant_Branch_CreatedAt");
            });

            modelBuilder.Entity<ExpenseInvoice>(b =>
            {
                b.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(e => new { e.TenantId, e.BranchId, e.InvoiceDate })
                    .HasDatabaseName("IX_ExpenseInvoices_Tenant_Branch_Date");

                b.HasIndex(e => new { e.TenantId, e.BranchId, e.Status, e.InvoiceDate })
                    .HasDatabaseName("IX_ExpenseInvoices_Tenant_Branch_Status_Date");
            });

            // Delivery zones are branch-owned: unique code per branch (active rows only, so
            // a soft-deleted code can be reused) + covering indexes for the per-branch
            // active-zone listing/selector.
            modelBuilder.Entity<DeliveryZone>(b =>
            {
                b.HasOne(z => z.Branch)
                    .WithMany()
                    .HasForeignKey(z => z.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(z => new { z.TenantId, z.BranchId, z.Code })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL");

                b.HasIndex(z => new { z.TenantId, z.BranchId, z.IsActive, z.DisplayOrder });

                b.HasIndex(z => new { z.TenantId, z.IsActive, z.CenterLatitude, z.CenterLongitude });
            });

            modelBuilder.Entity<DeliveryPartner>(b =>
            {
                b.Property(p => p.DefaultPricingRuleValue).HasColumnType("decimal(18,2)");
                b.Property(p => p.DefaultDeliveryCostValue).HasColumnType("decimal(18,2)");
                b.Property(p => p.CostSharingCommissionPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
                b.Property(p => p.CostSharingRestaurantPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(100m);
                b.Property(p => p.CostSharingCounterpartyPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
                b.Property(p => p.CostSharingMode).HasDefaultValue(CostSharingMode.RestaurantBearsAll);
                b.Property(p => p.CostSharingScope).HasDefaultValue(CostSharingScope.PerPartnerOrCard);

                b.HasIndex(p => new { p.TenantId, p.Code })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_DeliveryPartners_Tenant_Code");

                b.HasIndex(p => new { p.TenantId, p.Status, p.SortOrder })
                    .HasDatabaseName("IX_DeliveryPartners_Tenant_Status_Sort");
            });

            modelBuilder.Entity<DeliveryPartnerProduct>(b =>
            {
                b.Property(p => p.PricingRuleValue).HasColumnType("decimal(18,2)");
                b.Property(p => p.CustomPrice).HasColumnType("decimal(18,2)");

                b.HasOne(p => p.DeliveryPartner)
                    .WithMany(p => p.ProductMappings)
                    .HasForeignKey(p => p.DeliveryPartnerId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(p => p.Product)
                    .WithMany()
                    .HasForeignKey(p => p.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(p => new { p.TenantId, p.DeliveryPartnerId, p.ProductId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_DeliveryPartnerProducts_Tenant_Partner_Product");

                b.HasIndex(p => new { p.TenantId, p.DeliveryPartnerId, p.PartnerProductId })
                    .IsUnique()
                    .HasFilter("\"PartnerProductId\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_DeliveryPartnerProducts_Tenant_Partner_ExternalId");

                b.HasIndex(p => new { p.TenantId, p.ProductId, p.IsEnabled })
                    .HasDatabaseName("IX_DeliveryPartnerProducts_Tenant_Product_Enabled");
            });

            modelBuilder.Entity<DeliveryPartnerActivityLog>(b =>
            {
                b.HasOne(l => l.DeliveryPartner)
                    .WithMany()
                    .HasForeignKey(l => l.DeliveryPartnerId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(l => l.Product)
                    .WithMany()
                    .HasForeignKey(l => l.ProductId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(l => new { l.TenantId, l.DeliveryPartnerId, l.CreatedAt })
                    .HasDatabaseName("IX_DeliveryPartnerActivity_Tenant_Partner_Time");

                b.HasIndex(l => new { l.TenantId, l.ProductId, l.CreatedAt })
                    .HasDatabaseName("IX_DeliveryPartnerActivity_Tenant_Product_Time");
            });

            modelBuilder.Entity<CostSharingProvider>(b =>
            {
                b.HasIndex(p => new { p.TenantId, p.Code })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_CostSharingProviders_Tenant_Code");

                b.HasIndex(p => new { p.TenantId, p.Type, p.IsActive })
                    .HasDatabaseName("IX_CostSharingProviders_Tenant_Type_Active");
            });

            modelBuilder.Entity<CostSharingRule>(b =>
            {
                b.HasOne(r => r.Provider)
                    .WithMany(p => p.Rules)
                    .HasForeignKey(r => r.ProviderId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(r => r.DeliveryPartner)
                    .WithMany()
                    .HasForeignKey(r => r.DeliveryPartnerId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(r => r.PaymentMethod)
                    .WithMany()
                    .HasForeignKey(r => r.PaymentMethodId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(r => new { r.TenantId, r.ProviderType, r.Scope, r.IsActive })
                    .HasDatabaseName("IX_CostSharingRules_Tenant_Type_Scope_Active");

                b.HasIndex(r => new { r.TenantId, r.DeliveryPartnerId, r.IsActive })
                    .HasDatabaseName("IX_CostSharingRules_Tenant_Partner_Active");

                b.HasIndex(r => new { r.TenantId, r.PaymentMethodId, r.IsActive })
                    .HasDatabaseName("IX_CostSharingRules_Tenant_Payment_Active");
            });

            modelBuilder.Entity<CostSharingOverrideAudit>(b =>
            {
                b.HasOne(a => a.ApprovedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.ApprovedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(a => a.PerformedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.PerformedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(a => new { a.TenantId, a.OrderId, a.CreatedAt })
                    .HasDatabaseName("IX_CostSharingOverrideAudits_Tenant_Order_Time");
            });

            modelBuilder.Entity<OrderProfitabilitySnapshot>(b =>
            {
                b.HasOne(s => s.Order)
                    .WithOne(o => o.ProfitabilitySnapshot)
                    .HasForeignKey<OrderProfitabilitySnapshot>(s => s.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(s => s.OrderId)
                    .IsUnique()
                    .HasDatabaseName("IX_OrderProfitabilitySnapshots_Order");

                b.HasIndex(s => new { s.TenantId, s.PaidAt })
                    .HasDatabaseName("IX_OrderProfitabilitySnapshots_Tenant_PaidAt");

                b.HasIndex(s => new { s.TenantId, s.DeliveryPartnerId, s.PaidAt })
                    .HasDatabaseName("IX_OrderProfitabilitySnapshots_Tenant_Partner_PaidAt");

                b.HasIndex(s => new { s.TenantId, s.PaymentMethodId, s.PaidAt })
                    .HasDatabaseName("IX_OrderProfitabilitySnapshots_Tenant_Payment_PaidAt");

                b.HasIndex(s => new { s.TenantId, s.CashierId, s.PaidAt })
                    .HasDatabaseName("IX_OrderProfitabilitySnapshots_Tenant_Cashier_PaidAt");
            });

            modelBuilder.Entity<OrderProfitabilitySnapshotItem>(b =>
            {
                b.HasOne(i => i.Snapshot)
                    .WithMany(s => s.Items)
                    .HasForeignKey(i => i.SnapshotId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(i => new { i.TenantId, i.ProductId })
                    .HasDatabaseName("IX_OrderProfitabilitySnapshotItems_Tenant_Product");

                b.HasIndex(i => new { i.TenantId, i.SnapshotId })
                    .HasDatabaseName("IX_OrderProfitabilitySnapshotItems_Tenant_Snapshot");
            });

            modelBuilder.Entity<CustomerAccount>(b =>
            {
                b.HasIndex(c => new { c.TenantId, c.Email })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_CustomerAccounts_Tenant_Email");

                b.HasIndex(c => new { c.TenantId, c.MobileNumber })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_CustomerAccounts_Tenant_Mobile");

                b.HasIndex(c => new { c.TenantId, c.CustomerNumber })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_CustomerAccounts_Tenant_Number");

                // Phone-auth flow normalizes to PhoneNumber. Unique per tenant for active
                // accounts; partial filter keeps soft-deleted rows from blocking reuse.
                b.HasIndex(c => new { c.TenantId, c.PhoneNumber })
                    .IsUnique()
                    .HasFilter("\"PhoneNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_CustomerAccounts_Tenant_PhoneNumber");
            });

            modelBuilder.Entity<CustomerOtp>(b =>
            {
                b.HasOne(o => o.Customer)
                    .WithMany(c => c.Otps)
                    .HasForeignKey(o => o.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(o => new { o.TenantId, o.PhoneNumber, o.CreatedAt })
                    .HasDatabaseName("IX_CustomerOtps_Tenant_Phone_Created");

                b.HasIndex(o => new { o.TenantId, o.CustomerId, o.Purpose, o.UsedAt })
                    .HasDatabaseName("IX_CustomerOtps_Tenant_Customer_Purpose_Used");
            });

            modelBuilder.Entity<CustomerAddress>(b =>
            {
                b.HasOne(a => a.Customer)
                    .WithMany(c => c.Addresses)
                    .HasForeignKey(a => a.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(a => a.DeliveryZone)
                    .WithMany()
                    .HasForeignKey(a => a.DeliveryZoneId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(a => new { a.TenantId, a.CustomerId, a.IsDefault })
                    .HasDatabaseName("IX_CustomerAddresses_Tenant_Customer_Default");
            });

            modelBuilder.Entity<CustomerDevice>(b =>
            {
                b.Property(d => d.IsActive)
                    .HasDefaultValue(true);

                b.HasOne(d => d.Customer)
                    .WithMany(c => c.Devices)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(d => new { d.TenantId, d.CustomerId, d.DeviceId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_CustomerDevices_Tenant_Customer_Device");
            });

            modelBuilder.Entity<CustomerRefreshToken>(b =>
            {
                b.HasOne(t => t.Customer)
                    .WithMany(c => c.RefreshTokens)
                    .HasForeignKey(t => t.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(t => t.TokenHash)
                    .IsUnique()
                    .HasDatabaseName("IX_CustomerRefreshTokens_TokenHash");

                b.HasIndex(t => new { t.TenantId, t.CustomerId, t.DeviceId })
                    .HasDatabaseName("IX_CustomerRefreshTokens_Tenant_Customer_Device");
            });

            modelBuilder.Entity<CustomerCart>(b =>
            {
                b.HasOne(c => c.Customer)
                    .WithMany(c => c.Carts)
                    .HasForeignKey(c => c.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(c => new { c.TenantId, c.CustomerId, c.IsActive })
                    .IsUnique()
                    .HasFilter("\"IsActive\" = true AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_CustomerCarts_Tenant_Customer_Active");
            });

            modelBuilder.Entity<CustomerCartItem>(b =>
            {
                b.HasOne(i => i.Cart)
                    .WithMany(c => c.Items)
                    .HasForeignKey(i => i.CartId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(i => i.Product)
                    .WithMany()
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(i => i.SelectedOption)
                    .WithMany()
                    .HasForeignKey(i => i.SelectedOptionId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(i => new { i.TenantId, i.CartId })
                    .HasDatabaseName("IX_CustomerCartItems_Tenant_Cart");
            });

            modelBuilder.Entity<CustomerCartItemModifier>(b =>
            {
                b.HasOne(m => m.CartItem)
                    .WithMany(i => i.Modifiers)
                    .HasForeignKey(m => m.CartItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(m => m.Modifier)
                    .WithMany()
                    .HasForeignKey(m => m.ModifierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CustomerMobileOrder>(b =>
            {
                b.HasOne(o => o.Customer)
                    .WithMany()
                    .HasForeignKey(o => o.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(o => o.Order)
                    .WithMany()
                    .HasForeignKey(o => o.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(o => new { o.TenantId, o.CustomerId, o.CreatedAt })
                    .HasDatabaseName("IX_CustomerMobileOrders_Tenant_Customer_Created");

                b.HasIndex(o => new { o.TenantId, o.OrderId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_CustomerMobileOrders_Tenant_Order");
            });

            modelBuilder.Entity<CustomerMobileAuditLog>(b =>
            {
                b.HasOne(l => l.Customer)
                    .WithMany()
                    .HasForeignKey(l => l.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(l => new { l.TenantId, l.CustomerId, l.ActionAt })
                    .HasDatabaseName("IX_CustomerMobileAuditLogs_Tenant_Customer_ActionAt");

                b.HasIndex(l => new { l.TenantId, l.OrderId })
                    .HasDatabaseName("IX_CustomerMobileAuditLogs_Tenant_Order");

                b.HasIndex(l => new { l.TenantId, l.NotificationId })
                    .HasDatabaseName("IX_CustomerMobileAuditLogs_Tenant_Notification");
            });

            // Optional FK Order -> DeliveryZone. SetNull keeps historical orders valid if a
            // zone is ever removed; chargeable values are snapshotted on the order anyway, so
            // reporting never depends on the live zone row. Index supports delivery reporting.
            modelBuilder.Entity<Order>()
                .HasOne<DeliveryZone>()
                .WithMany()
                .HasForeignKey(o => o.DeliveryZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.DeliveryZoneId });

            modelBuilder.Entity<Order>(b =>
            {
                b.HasOne(o => o.DeliveryPartner)
                    .WithMany()
                    .HasForeignKey(o => o.DeliveryPartnerId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.Property(o => o.PartnerDeliveryFee).HasColumnType("decimal(18,2)");
                b.Property(o => o.PartnerServiceFee).HasColumnType("decimal(18,2)");
                b.Property(o => o.FoodSubtotal).HasColumnType("decimal(18,2)");
                b.Property(o => o.CustomerDeliveryFee).HasColumnType("decimal(18,2)");
                b.Property(o => o.ActualDeliveryCost).HasColumnType("decimal(18,2)");
                b.Property(o => o.DeliveryMargin).HasColumnType("decimal(18,2)");
                b.Property(o => o.MarketplaceDeliveryFee).HasColumnType("decimal(18,2)");
                b.Property(o => o.MarketplaceServiceFee).HasColumnType("decimal(18,2)");
                b.Property(o => o.NetRestaurantRevenue).HasColumnType("decimal(18,2)");
                b.Property(o => o.CostSharingTotalCommission).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                b.Property(o => o.CostSharingRestaurantShare).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                b.Property(o => o.CostSharingCounterpartyShare).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                b.Property(o => o.CostSharingNetSettlement).HasColumnType("decimal(18,2)").HasDefaultValue(0m);

                b.HasIndex(o => new { o.TenantId, o.DeliveryPartnerId, o.CreatedAt })
                    .HasDatabaseName("IX_Orders_Tenant_Partner_CreatedAt");

                b.HasIndex(o => new { o.TenantId, o.DeliveryPartnerId, o.PartnerOrderNumber })
                    .IsUnique()
                    .HasFilter("\"DeliveryPartnerId\" IS NOT NULL AND \"PartnerOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_Orders_Tenant_Partner_OrderNumber");

                b.HasIndex(o => new { o.TenantId, o.OrderSource, o.CreatedAt })
                    .HasDatabaseName("IX_Orders_Tenant_Source_CreatedAt");
            });

            modelBuilder.Entity<OrderItem>(b =>
            {
                b.Property(i => i.UnitPriceSnapshot).HasColumnType("decimal(18,2)");
                b.Property(i => i.LineTotalSnapshot).HasColumnType("decimal(18,2)");
                b.Property(i => i.PartnerPriceSnapshot).HasColumnType("decimal(18,2)");
                b.Property(i => i.PartnerOriginalUnitPrice).HasColumnType("decimal(18,2)");
                b.Property(i => i.PartnerDiscountedUnitPrice).HasColumnType("decimal(18,2)");

                b.HasOne(i => i.PartnerDiscountUpdatedByUser)
                    .WithMany()
                    .HasForeignKey(i => i.PartnerDiscountUpdatedBy)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(i => new { i.TenantId, i.HasPartnerDiscountOverride })
                    .HasDatabaseName("IX_OrderItems_Tenant_PartnerDiscountOverride");
            });

            modelBuilder.Entity<PartnerPriceOverrideAudit>(b =>
            {
                b.Property(a => a.OldPrice).HasColumnType("decimal(18,2)");
                b.Property(a => a.NewPrice).HasColumnType("decimal(18,2)");
                b.Property(a => a.OldDiscountAmount).HasColumnType("decimal(18,2)");
                b.Property(a => a.NewDiscountAmount).HasColumnType("decimal(18,2)");

                b.HasIndex(a => new { a.TenantId, a.OrderId, a.UpdatedAt })
                    .HasDatabaseName("IX_PartnerPriceOverrideAudit_Tenant_Order_Time");

                b.HasIndex(a => new { a.TenantId, a.OrderItemId, a.UpdatedAt })
                    .HasDatabaseName("IX_PartnerPriceOverrideAudit_Tenant_Item_Time");
            });

            // Configure Many-to-Many Relationship Bridge
            modelBuilder.Entity<ProductModifierGroup>()
                .HasKey(pmg => new { pmg.ProductId, pmg.ModifierGroupId });

            modelBuilder.Entity<ProductModifierGroup>()
                .HasOne(pmg => pmg.Product)
                .WithMany(p => p.ProductModifierGroups)
                .HasForeignKey(pmg => pmg.ProductId);

            modelBuilder.Entity<ProductModifierGroup>()
                .HasOne(pmg => pmg.ModifierGroup)
                .WithMany(mg => mg.ProductGroups)
                .HasForeignKey(pmg => pmg.ModifierGroupId);

            // Configure Order Relationships
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasMany(o => o.Payments)
                .WithOne(p => p.Order)
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentMethod>(b =>
            {
                b.Property(pm => pm.CostSharingCommissionPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
                b.Property(pm => pm.CostSharingRestaurantPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(100m);
                b.Property(pm => pm.CostSharingCounterpartyPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
                b.Property(pm => pm.CostSharingMode).HasDefaultValue(CostSharingMode.RestaurantBearsAll);
                b.Property(pm => pm.CostSharingScope).HasDefaultValue(CostSharingScope.PerPartnerOrCard);

                b.HasIndex(pm => new { pm.TenantId, pm.Code })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_PaymentMethods_Tenant_Code");

                b.HasIndex(pm => new { pm.TenantId, pm.IsActive, pm.DisplayOrder })
                    .HasDatabaseName("IX_PaymentMethods_Tenant_Active_Order");

                b.HasIndex(pm => new { pm.TenantId, pm.IsDefault })
                    .IsUnique()
                    .HasFilter("\"IsDefault\" = true AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_PaymentMethods_Tenant_Default");
            });

            modelBuilder.Entity<PublicReceiptToken>()
                .HasIndex(t => t.Token)
                .IsUnique()
                .HasDatabaseName("IX_PublicReceiptTokens_Token");

            modelBuilder.Entity<PublicReceiptToken>()
                .HasIndex(t => new { t.TenantId, t.OrderId, t.IsActive })
                .HasDatabaseName("IX_PublicReceiptTokens_Tenant_Order_Active");

            modelBuilder.Entity<Order>()
                .HasMany(o => o.VoucherUsageAudits)
                .WithOne(v => v.Order)
                .HasForeignKey(v => v.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.SetNull); // Müşteri silinirse siparişler kalsın

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Offer)
                .WithMany(o => o.Orders)
                .HasForeignKey(o => o.OfferId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.PaidByUser)
                .WithMany()
                .HasForeignKey(o => o.PaidByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.CanceledBy)
                .WithMany()
                .HasForeignKey(o => o.CanceledById)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.PaidByUserId, o.PaidAt });

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.ClientOrderUuid })
                .IsUnique()
                .HasFilter("\"ClientOrderUuid\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Orders_Tenant_ClientOrderUuid");

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.PublicOrderNumber })
                .HasFilter("\"PublicOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Orders_Tenant_PublicOrderNumber");

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.OrderNumber })
                .IsUnique()
                .HasDatabaseName("UX_Orders_Tenant_OrderNumber");

            // Display numbers may repeat after their configured reset boundary.
            // OrderNumber remains the unique internal system reference.
            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.DisplayOrderNumber })
                .HasFilter("\"DisplayOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Orders_Tenant_DisplayOrderNumber");

            modelBuilder.Entity<OrderDisplaySequence>(b =>
            {
                // Full unique constraint (no partial filter) so Postgres ON CONFLICT
                // can target it for the atomic upsert+increment used by the generator.
                // BucketKey replaces the old (Year, Month) tuple so the same table can
                // serve any reset strategy (Never/Daily/Monthly/Yearly/Shift).
                b.HasIndex(s => new { s.TenantId, s.OrderTypeCode, s.BucketKey })
                    .IsUnique()
                    .HasDatabaseName("UX_OrderDisplaySequences_Tenant_Code_Bucket");
            });

            modelBuilder.Entity<OrderNumberingConfig>(b =>
            {
                // One config per tenant. Partial filter excludes soft-deleted rows.
                b.HasIndex(c => c.TenantId)
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("UX_OrderNumberingConfigs_Tenant");
            });

            modelBuilder.Entity<ShiftRulesConfig>(b =>
            {
                b.HasIndex(c => c.TenantId)
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("UX_ShiftRulesConfigs_Tenant");
            });

            modelBuilder.Entity<ConfigAuditLog>(b =>
            {
                b.HasIndex(c => new { c.TenantId, c.CreatedAt })
                    .HasDatabaseName("IX_ConfigAuditLogs_Tenant_CreatedAt");
                b.HasIndex(c => new { c.TenantId, c.EventType, c.CreatedAt })
                    .HasDatabaseName("IX_ConfigAuditLogs_Tenant_EventType_CreatedAt");
            });

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.TalabatOrderNumber })
                .HasFilter("\"TalabatOrderNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Orders_Tenant_TalabatOrderNumber");

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.IsVoucherApplied, o.VoucherAppliedAt })
                .HasDatabaseName("IX_Orders_Tenant_Voucher");

            // Index for efficient today/date-range filtering used by cashier endpoint
            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.CreatedAt })
                .HasDatabaseName("IX_Orders_TenantId_CreatedAt");

            // Composite index for the takeaway active board query:
            // WHERE OrderType = Takeaway AND Status != Cancelled ORDER BY CreatedAt DESC
            // Covers both the filter predicates and the sort in a single index scan.
            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.OrderType, o.Status, o.CreatedAt })
                .HasDatabaseName("IX_Orders_TakeawayActive");

            // Composite index for the kitchen (KDS) today/all query:
            // WHERE TenantId = X AND (CreatedAt >= today OR (CreatedAt < today AND Status NOT IN (...)))
            // Status + CreatedAt together cover both branches of the OR predicate.
            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.TenantId, o.Status, o.CreatedAt })
                .HasDatabaseName("IX_Orders_Kitchen");

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.CreatedByUser)
                .WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.PaymentMethod)
                .WithMany(pm => pm.Payments)
                .HasForeignKey(p => p.PaymentMethodId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Payment>(b =>
            {
                b.Property(p => p.CostSharingCommissionPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
                b.Property(p => p.CostSharingRestaurantPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(100m);
                b.Property(p => p.CostSharingCounterpartyPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0m);
                b.Property(p => p.CostSharingCommissionAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                b.Property(p => p.CostSharingRestaurantShareAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                b.Property(p => p.CostSharingCounterpartyShareAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                b.Property(p => p.CostSharingMode).HasDefaultValue(CostSharingMode.RestaurantBearsAll);
                b.Property(p => p.CostSharingScope).HasDefaultValue(CostSharingScope.PerPartnerOrCard);
            });

            modelBuilder.Entity<Payment>()
                .HasIndex(p => new { p.TenantId, p.OrderId, p.CreatedAt })
                .HasDatabaseName("IX_Payments_Order");

            modelBuilder.Entity<Payment>()
                .HasIndex(p => new { p.TenantId, p.PaymentMethodId, p.CreatedAt })
                .HasDatabaseName("IX_Payments_Tenant_PaymentMethod_CreatedAt");

            modelBuilder.Entity<Payment>()
                .HasIndex(p => new { p.TenantId, p.CreatedByUserId, p.CreatedAt })
                .HasDatabaseName("IX_Payments_CreatedByUser");

            modelBuilder.Entity<Payment>()
                .HasIndex(p => new { p.TenantId, p.ClientActionId })
                .IsUnique()
                .HasFilter("\"ClientActionId\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Payments_Tenant_ClientActionId");

            modelBuilder.Entity<PaymentAdjustment>(b =>
            {
                b.HasOne(a => a.Order)
                    .WithMany()
                    .HasForeignKey(a => a.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(a => a.RequestedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.RequestedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(a => a.ApprovedByUser)
                    .WithMany()
                    .HasForeignKey(a => a.ApprovedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(a => new { a.TenantId, a.OrderId, a.ApprovedAt })
                    .HasDatabaseName("IX_PaymentAdjustments_Tenant_Order_ApprovedAt");

                b.HasIndex(a => new { a.TenantId, a.ApprovedAt })
                    .HasDatabaseName("IX_PaymentAdjustments_Tenant_ApprovedAt");

                b.HasIndex(a => new { a.TenantId, a.OrderId, a.Status })
                    .IsUnique()
                    .HasFilter("\"Status\" = 0 AND \"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_PaymentAdjustments_Tenant_Order_Status");
            });

            modelBuilder.Entity<PaymentAdjustmentDetail>(b =>
            {
                b.Property(d => d.Amount).HasColumnType("decimal(18,2)");

                b.HasOne(d => d.Adjustment)
                    .WithMany(a => a.Details)
                    .HasForeignKey(d => d.AdjustmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(d => d.Payment)
                    .WithMany()
                    .HasForeignKey(d => d.PaymentId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(d => d.NewPayment)
                    .WithMany()
                    .HasForeignKey(d => d.NewPaymentId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(d => d.OldPaymentMethod)
                    .WithMany()
                    .HasForeignKey(d => d.OldPaymentMethodId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(d => d.NewPaymentMethod)
                    .WithMany()
                    .HasForeignKey(d => d.NewPaymentMethodId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(d => new { d.TenantId, d.AdjustmentId })
                    .HasDatabaseName("IX_PaymentAdjustmentDetails_Tenant_Adjustment");

                b.HasIndex(d => new { d.TenantId, d.OldPaymentMethodId, d.NewPaymentMethodId })
                    .HasDatabaseName("IX_PaymentAdjustmentDetails_Tenant_Methods");
            });

            modelBuilder.Entity<VoucherUsageAudit>()
                .HasOne(v => v.CreatedByUser)
                .WithMany()
                .HasForeignKey(v => v.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<VoucherUsageAudit>()
                .HasIndex(v => new { v.TenantId, v.OrderId })
                .IsUnique()
                .HasDatabaseName("IX_VoucherUsageAudits_Tenant_Order");

            modelBuilder.Entity<VoucherUsageAudit>()
                .HasIndex(v => new { v.TenantId, v.BusinessDate, v.UsedAt })
                .HasDatabaseName("IX_VoucherUsageAudits_Tenant_BusinessDate");

            modelBuilder.Entity<SystemSettings>()
                .Property(s => s.VoucherEnabled)
                .HasDefaultValue(VoucherDefaults.Enabled);

            modelBuilder.Entity<SystemSettings>()
                .Property(s => s.VoucherAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(VoucherDefaults.Amount);

            modelBuilder.Entity<SystemSettings>()
                .Property(s => s.VoucherDailyLimit)
                .HasDefaultValue(VoucherDefaults.DailyLimit);

            modelBuilder.Entity<SystemSettings>()
                .Property(s => s.DuplicateInvoiceBehavior)
                .HasDefaultValue(DuplicateInvoiceBehavior.WarningOnly);

            modelBuilder.Entity<SystemSettings>(b =>
            {
                b.Property(s => s.AllowMobileCancelPreparing)
                    .HasDefaultValue(false);

                b.Property(s => s.TimeZoneId)
                    .HasDefaultValue(RestaurantTimeDefaults.TimeZoneId);

                b.Property(s => s.LoyaltyPointValue)
                    .HasColumnType("decimal(18,4)")
                    .HasDefaultValue(0m);

                b.Property(s => s.TermsTitle)
                    .HasDefaultValue("Terms & Conditions");

                b.Property(s => s.TermsTitleAr)
                    .HasDefaultValue("الشروط والأحكام");

                b.Property(s => s.PrivacyTitle)
                    .HasDefaultValue("Privacy Policy");

                b.Property(s => s.PrivacyTitleAr)
                    .HasDefaultValue("سياسة الخصوصية");
            });

            modelBuilder.Entity<FollowUsClick>(b =>
            {
                b.HasIndex(c => new { c.TenantId, c.ClickedAt })
                    .HasDatabaseName("IX_FollowUsClicks_Tenant_ClickedAt");

                b.HasIndex(c => new { c.TenantId, c.Platform, c.ClickedAt })
                    .HasDatabaseName("IX_FollowUsClicks_Tenant_Platform_ClickedAt");
            });

            modelBuilder.Entity<OrderItem>()
                .HasMany(oi => oi.Modifiers)
                .WithOne(oim => oim.OrderItem)
                .HasForeignKey(oim => oim.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasMany(oi => oi.RecipeSnapshotItems)
                .WithOne(rs => rs.OrderItem)
                .HasForeignKey(rs => rs.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Configure 1-to-1 User <-> StaffProfile
            modelBuilder.Entity<User>()
                .HasOne(u => u.StaffProfile)
                .WithOne(sp => sp.User)
                .HasForeignKey<StaffProfile>(sp => sp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Configure StaffProfile -> TimeEntries
            modelBuilder.Entity<StaffProfile>()
                .HasMany(sp => sp.TimeEntries)
                .WithOne(te => te.Staff)
                .HasForeignKey(te => te.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.UserId);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.TokenHash)
                .IsUnique();

            modelBuilder.Entity<TimeEntry>()
                .HasIndex(te => new { te.StaffId, te.ClockIn });

            modelBuilder.Entity<MobileLeaveRequest>()
                .HasOne(r => r.StaffProfile)
                .WithMany()
                .HasForeignKey(r => r.StaffProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MobileLeaveRequest>()
                .HasIndex(r => new { r.TenantId, r.StaffProfileId, r.CreatedAt });

            modelBuilder.Entity<MobileLeaveRequest>()
                .HasIndex(r => new { r.TenantId, r.StaffProfileId, r.Status, r.StartDate, r.EndDate });

            modelBuilder.Entity<MobileLoanRequest>()
                .HasOne(r => r.StaffProfile)
                .WithMany()
                .HasForeignKey(r => r.StaffProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MobileLoanRequest>()
                .HasIndex(r => new { r.TenantId, r.StaffProfileId, r.CreatedAt });

            modelBuilder.Entity<MobileLoanRequest>()
                .HasIndex(r => new { r.TenantId, r.StaffProfileId, r.Status, r.RequestDate });

            modelBuilder.Entity<MobilePermissionRequest>()
                .HasOne(r => r.StaffProfile)
                .WithMany()
                .HasForeignKey(r => r.StaffProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MobilePermissionRequest>()
                .HasIndex(r => new { r.TenantId, r.StaffProfileId, r.CreatedAt });

            modelBuilder.Entity<MobilePermissionRequest>()
                .HasIndex(r => new { r.TenantId, r.StaffProfileId, r.Status, r.Date });

            modelBuilder.Entity<Bonus>()
                .HasOne(b => b.StaffProfile)
                .WithMany()
                .HasForeignKey(b => b.StaffProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Bonus>()
                .HasIndex(b => new { b.TenantId, b.StaffProfileId, b.AwardDate });

            modelBuilder.Entity<Bonus>()
                .HasIndex(b => new { b.TenantId, b.Status, b.AwardDate });

            // Configure RecipeItem Relationships
            modelBuilder.Entity<RecipeItem>()
                .HasOne(r => r.Product)
                .WithMany(p => p.RecipeItems)
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure ProductOption relationships
            modelBuilder.Entity<ProductOption>()
                .HasOne(o => o.Product)
                .WithMany(p => p.Options)
                .HasForeignKey(o => o.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductOptionRecipeItem>()
                .HasOne(r => r.ProductOption)
                .WithMany(o => o.RecipeItems)
                .HasForeignKey(r => r.ProductOptionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductOptionRecipeItem>()
                .HasOne(r => r.RawMaterial)
                .WithMany()
                .HasForeignKey(r => r.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Modifier>()
                .HasOne(m => m.LinkedProduct)
                .WithMany()
                .HasForeignKey(m => m.LinkedProductId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure TableCategory -> Tables
            modelBuilder.Entity<Table>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Tables)
                .HasForeignKey(t => t.TableCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Table>(b =>
            {
                b.HasOne(t => t.Branch)
                    .WithMany()
                    .HasForeignKey(t => t.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(t => new { t.TenantId, t.BranchId, t.Name })
                    .HasDatabaseName("IX_Tables_Tenant_Branch_Name");
            });

            modelBuilder.Entity<TableCategory>(b =>
            {
                b.HasOne(c => c.Branch)
                    .WithMany()
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(c => new { c.TenantId, c.BranchId, c.Name })
                    .HasDatabaseName("IX_TableCategories_Tenant_Branch_Name");
            });

            modelBuilder.Entity<RecipeItem>()
                .HasOne(r => r.RawMaterial)
                .WithMany() // RawMaterial has no collection of RecipeItems
                .HasForeignKey(r => r.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting RawMaterial if it's used in a recipe

            // Ingredient-level alternatives (per RecipeItem swap list)
            modelBuilder.Entity<RecipeItemAlternative>()
                .HasOne(a => a.RecipeItem)
                .WithMany(ri => ri.Alternatives)
                .HasForeignKey(a => a.RecipeItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RecipeItemAlternative>()
                .HasOne(a => a.RawMaterial)
                .WithMany()
                .HasForeignKey(a => a.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RecipeItemAlternative>()
                .Property(a => a.PricingType)
                .HasDefaultValue(RecipeItemAlternativePricingType.PriceDifference)
                .HasSentinel((RecipeItemAlternativePricingType)(-1));

            modelBuilder.Entity<ModifierRecipeItem>()
                .HasOne(r => r.Modifier)
                .WithMany(m => m.RecipeItems)
                .HasForeignKey(r => r.ModifierId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ModifierRecipeItem>()
                .HasOne(r => r.RawMaterial)
                .WithMany()
                .HasForeignKey(r => r.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItemRecipeSnapshot>()
                .HasOne(r => r.RawMaterial)
                .WithMany()
                .HasForeignKey(r => r.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OfferProduct>()
                .HasOne(op => op.Offer)
                .WithMany(o => o.OfferProducts)
                .HasForeignKey(op => op.OfferId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OfferProduct>()
                .HasOne(op => op.Product)
                .WithMany(p => p.OfferProducts)
                .HasForeignKey(op => op.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Configure PurchaseOrder Relationships
            modelBuilder.Entity<PurchaseOrder>()
                .HasMany(po => po.Items)
                .WithOne(poi => poi.PurchaseOrder)
                .HasForeignKey(poi => poi.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(po => po.Supplier)
                .WithMany()
                .HasForeignKey(po => po.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockBatch>()
                .HasOne(sb => sb.RawMaterial)
                .WithMany()
                .HasForeignKey(sb => sb.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockBatch>()
                .HasOne(sb => sb.PurchaseOrder)
                .WithMany()
                .HasForeignKey(sb => sb.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);
            // CashierBalanceShift relationships
            modelBuilder.Entity<CashierBalanceShift>()
                .HasOne(c => c.Cashier)
                .WithMany()
                .HasForeignKey(c => c.CashierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CashierBalanceShift>()
                .HasOne(c => c.OpenedByManager)
                .WithMany()
                .HasForeignKey(c => c.OpenedByManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CashierBalanceShift>()
                .HasIndex(c => new { c.TenantId, c.CashierId, c.OpenedAt })
                .HasDatabaseName("IX_CashierBalanceShifts_Tenant_Cashier_OpenedAt");

            modelBuilder.Entity<CashierBalanceShift>()
                .HasIndex(c => new { c.TenantId, c.OpenTransactionId })
                .IsUnique()
                .HasFilter("\"OpenTransactionId\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_CashierBalanceShifts_Tenant_OpenTx");

            modelBuilder.Entity<CashierBalanceShift>()
                .HasIndex(c => new { c.TenantId, c.CloseTransactionId })
                .IsUnique()
                .HasFilter("\"CloseTransactionId\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_CashierBalanceShifts_Tenant_CloseTx");

            // ── WasteLog ──────────────────────────────────────────────────────────
            modelBuilder.Entity<WasteLog>()
                .Property(w => w.Type)
                .HasDefaultValue(WasteLogType.Waste)
                .HasSentinel((WasteLogType)(-1));

            modelBuilder.Entity<WasteLog>()
                .Property(w => w.Status)
                .HasDefaultValue(WasteLogStatus.Approved)
                .HasSentinel((WasteLogStatus)(-1));

            modelBuilder.Entity<WasteLog>()
                .Property(w => w.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<WasteLog>()
                .Property(w => w.CostAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<WasteLog>()
                .Property(w => w.SalePriceLoss)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<WasteLog>()
                .HasOne(w => w.LoggedBy)
                .WithMany()
                .HasForeignKey(w => w.LoggedById)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WasteLog>()
                .HasOne(w => w.ApprovedByUser)
                .WithMany()
                .HasForeignKey(w => w.ApprovedById)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WasteLog>()
                .HasOne(w => w.Product)
                .WithMany()
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WasteLog>()
                .HasOne(w => w.Material)
                .WithMany()
                .HasForeignKey(w => w.MaterialId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WasteLog>()
                .HasOne(w => w.SourceOrder)
                .WithMany()
                .HasForeignKey(w => w.SourceOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WasteLog>()
                .HasOne(w => w.SourceOrderItem)
                .WithMany()
                .HasForeignKey(w => w.SourceOrderItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WasteLog>()
                .HasOne(w => w.Shift)
                .WithMany()
                .HasForeignKey(w => w.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for WasteLog
            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.CreatedAt });

            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.WasteDate });

            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.Category, w.CreatedAt });

            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.Type, w.ShiftId, w.CreatedAt });

            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.WasteNumber })
                .IsUnique()
                .HasFilter("\"WasteNumber\" IS NOT NULL");

            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.Status, w.CreatedAt });

            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.WasteType, w.CreatedAt });

            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.ProductId, w.CreatedAt });

            modelBuilder.Entity<WasteLog>()
                .HasIndex(w => new { w.TenantId, w.LoggedById, w.CreatedAt });

            // Staff-meal participants (multi-select). One WasteLog → many WasteLogEmployees.
            modelBuilder.Entity<WasteLogEmployee>()
                .HasOne(e => e.WasteLog)
                .WithMany(w => w.Employees)
                .HasForeignKey(e => e.WasteLogId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WasteLogEmployee>()
                .HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WasteLogEmployee>()
                .HasIndex(e => e.WasteLogId);

            modelBuilder.Entity<WasteLogEmployee>()
                .HasIndex(e => new { e.TenantId, e.EmployeeId, e.CreatedAt })
                .HasFilter("\"EmployeeId\" IS NOT NULL");

            modelBuilder.Entity<WasteLogAudit>()
                .HasOne(a => a.WasteLog)
                .WithMany(w => w.AuditTrail)
                .HasForeignKey(a => a.WasteLogId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WasteLogAudit>()
                .HasOne(a => a.PerformedBy)
                .WithMany()
                .HasForeignKey(a => a.PerformedById)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WasteLogAudit>()
                .HasIndex(a => new { a.TenantId, a.WasteLogId, a.CreatedAt });

            modelBuilder.Entity<InventoryTransaction>()
                .Property(t => t.CostAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<InventoryTransaction>()
                .Property(t => t.Quantity)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.WasteLog)
                .WithMany(w => w.InventoryTransactions)
                .HasForeignKey(t => t.WasteLogId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.Product)
                .WithMany()
                .HasForeignKey(t => t.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOne(t => t.Material)
                .WithMany()
                .HasForeignKey(t => t.MaterialId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryTransaction>()
                .HasIndex(t => new { t.TenantId, t.TransactionType, t.CreatedAt });

            modelBuilder.Entity<InventoryTransaction>()
                .HasIndex(t => new { t.TenantId, t.WasteLogId });

            // ── CancelLog ─────────────────────────────────────────────────────────
            modelBuilder.Entity<CancelLog>()
                .HasOne(c => c.Order)
                .WithMany()
                .HasForeignKey(c => c.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CancelLog>()
                .Property(c => c.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CancelLog>()
                .HasOne(c => c.OrderItem)
                .WithMany()
                .HasForeignKey(c => c.OrderItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CancelLog>()
                .HasOne(c => c.CancelledBy)
                .WithMany()
                .HasForeignKey(c => c.CancelledById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CancelLog>()
                .HasOne(c => c.WasteLog)
                .WithMany()
                .HasForeignKey(c => c.WasteLogId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CancelLog>()
                .HasOne(c => c.Shift)
                .WithMany()
                .HasForeignKey(c => c.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for CancelLog
            modelBuilder.Entity<CancelLog>()
                .HasIndex(c => new { c.TenantId, c.CancelledAt });

            modelBuilder.Entity<CancelLog>()
                .HasIndex(c => new { c.CancelledById, c.CancelledAt });

            modelBuilder.Entity<CancelLog>()
                .HasIndex(c => new { c.TenantId, c.ShiftId, c.CancelledAt });

            // ── CancelReason (not tenant-filtered — has optional TenantId) ────────
            modelBuilder.Entity<CancelReason>()
                .HasIndex(cr => cr.Code);

            // ── StockBatch expiry indexes ─────────────────────────────────────────
            modelBuilder.Entity<StockBatch>()
                .HasIndex(sb => sb.ExpiryDate);

            // Compound index for tenant-scoped expiry reports filtered by status
            // (e.g. "all Good batches expiring in 7 days for tenant X").
            modelBuilder.Entity<StockBatch>()
                .HasIndex(sb => new { sb.TenantId, sb.Status, sb.ExpiryDate })
                .HasDatabaseName("IX_StockBatches_Tenant_Status_Expiry");

            // ── Customer phone uniqueness per tenant ──────────────────────────────
            // Partial unique index: only enforced when PhoneNumber is non-null.
            // Customer lookup by phone is the cashier's primary identification flow.
            modelBuilder.Entity<Customer>()
                .HasIndex(c => new { c.TenantId, c.PhoneNumber })
                .IsUnique()
                .HasFilter("\"PhoneNumber\" IS NOT NULL")
                .HasDatabaseName("IX_Customers_Tenant_Phone");

            // ── User username uniqueness per tenant ───────────────────────────────
            // Login query filters by (TenantId, Username); without this index it scans the table.
            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.TenantId, u.Username })
                .IsUnique()
                .HasDatabaseName("IX_Users_Tenant_Username");

            // ── Notification unread feed ──────────────────────────────────────────
            // Drives the bell-icon "unread, newest first" query on every page load.
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.TenantId, n.IsRead, n.CreatedAt })
                .HasDatabaseName("IX_Notifications_Tenant_IsRead_CreatedAt");

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.TenantId, n.CustomerId, n.IsRead, n.CreatedAt })
                .HasDatabaseName("IX_Notifications_Tenant_Customer_IsRead_CreatedAt");

            // ── Multi-kitchen routing ─────────────────────────────────────────────
            // Product → Kitchen: nullable FK, SetNull on delete so deleting a
            // kitchen never cascades into product loss. Existing products with
            // KitchenId == null fall back to the default-kitchen resolver.
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Kitchen)
                .WithMany(k => k.Products)
                .HasForeignKey(p => p.KitchenId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Subcategory>(b =>
            {
                b.HasOne(s => s.Category)
                    .WithMany(c => c.Subcategories)
                    .HasForeignKey(s => s.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasIndex(s => new { s.TenantId, s.CategoryId, s.DisplayOrder })
                    .HasDatabaseName("IX_Subcategories_Tenant_Category_DisplayOrder");

                b.HasIndex(s => new { s.TenantId, s.CategoryId, s.Name })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_Subcategories_Tenant_Category_Name");
            });

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Subcategory)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SubcategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.TenantId, p.KitchenId })
                .HasDatabaseName("IX_Products_Tenant_Kitchen");

            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.TenantId, p.CategoryId, p.SubcategoryId })
                .HasDatabaseName("IX_Products_Tenant_Category_Subcategory");

            modelBuilder.Entity<Product>()
                .Property(p => p.PricingMode)
                .HasDefaultValue(ProductPricingModes.Manual);

            // Printer → Kitchen: nullable so receipt printers can sit at the
            // counter without a kitchen. SetNull keeps printers valid if a
            // kitchen is removed; admin can re-assign.
            modelBuilder.Entity<Printer>()
                .HasOne(p => p.Kitchen)
                .WithMany(k => k.Printers)
                .HasForeignKey(p => p.KitchenId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Printer>()
                .HasIndex(p => new { p.TenantId, p.KitchenId, p.IsActive })
                .HasDatabaseName("IX_Printers_Tenant_Kitchen_Active");

            modelBuilder.Entity<Printer>()
                .HasIndex(p => new { p.TenantId, p.IsReceiptPrinter, p.IsActive })
                .HasDatabaseName("IX_Printers_Tenant_Receipt_Active");

            // Drives the "default receipt printer for tenant" lookup.
            // Partial filter on IsDefault keeps the index tiny (<= 1 row per tenant
            // expected; controller enforces the invariant on save).
            modelBuilder.Entity<Printer>()
                .HasIndex(p => new { p.TenantId, p.IsDefault, p.IsActive })
                .HasFilter("\"IsDefault\" = true")
                .HasDatabaseName("IX_Printers_Tenant_Default");

            // User → ReceiptPrinter: nullable, SetNull on delete so removing a
            // printer never wipes a user record. No cascade — fully additive.
            modelBuilder.Entity<User>()
                .HasOne(u => u.ReceiptPrinter)
                .WithMany()
                .HasForeignKey(u => u.ReceiptPrinterId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.TenantId, u.ReceiptPrinterId })
                .HasDatabaseName("IX_Users_Tenant_ReceiptPrinter");

            // PrintJob — drives the queue worker. Critical indexes:
            //   1. (TenantId, Status, NextAttemptAt) — the main "pick next due"
            //      query. Covers Pending + Failed-with-retry-due in one scan.
            //   2. IdempotencyKey unique per tenant — enforces no-duplicate-tickets
            //      even if upstream events fire twice.
            modelBuilder.Entity<PrintJob>()
                .HasIndex(j => new { j.TenantId, j.Status, j.NextAttemptAt })
                .HasDatabaseName("IX_PrintJobs_Tenant_Status_Next");

            modelBuilder.Entity<PrintJob>()
                .HasIndex(j => new { j.TenantId, j.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("IX_PrintJobs_Tenant_Idempotency");

            modelBuilder.Entity<PrintJob>()
                .HasIndex(j => new { j.TenantId, j.OrderId })
                .HasDatabaseName("IX_PrintJobs_Tenant_Order");

            modelBuilder.Entity<IdempotencyEntry>()
                .HasIndex(i => new { i.TenantId, i.Key })
                .IsUnique()
                .HasDatabaseName("IX_IdempotencyEntries_Tenant_Key");

            modelBuilder.Entity<IdempotencyEntry>()
                .HasIndex(i => new { i.TenantId, i.State, i.ProcessingExpiresAt })
                .HasDatabaseName("IX_IdempotencyEntries_Tenant_State_Expires");

            // ── ExpenseInvoice ────────────────────────────────────────────────────
            // FK -> User (CreatedBy): SetNull so deleting a user never wipes the
            // financial record. The user's display name is captured at write-time
            // by the service layer if a richer audit trail is later required.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            // FK -> existing Supplier (Procurement module). SetNull on delete so
            // financial history survives a supplier wipe. The denormalised
            // SupplierName column keeps the historical record readable.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ExpenseInvoice>()
                .HasOne(e => e.PurchaseOrder)
                .WithOne(o => o.SupplierInvoice)
                .HasForeignKey<ExpenseInvoice>(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => e.PurchaseOrderId)
                .IsUnique()
                .HasFilter("\"PurchaseOrderId\" IS NOT NULL")
                .HasDatabaseName("IX_ExpenseInvoices_PurchaseOrder");

            // FK -> ExpenseCategory. SetNull preserves historical CategoryLabel.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasOne(e => e.ExpenseCategory)
                .WithMany()
                .HasForeignKey(e => e.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // FK -> CashierBalanceShift. Non-null only for cashier shift expenses.
            // SetNull keeps the financial record alive if a shift row is ever removed.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasOne<CashierBalanceShift>()
                .WithMany()
                .HasForeignKey(e => e.CashierShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            // FK -> configurable PaymentMethods registry. Restrict is wrong here
            // (would block retiring a method); SetNull keeps the snapshot label in
            // PaymentMethodDetail as the readable record.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasOne<PaymentMethod>()
                .WithMany()
                .HasForeignKey(e => e.PaymentMethodId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ExpenseInvoice>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.CancelledById)
                .OnDelete(DeleteBehavior.SetNull);

            // Drives every shift-expense read: totals, list and Z-report block.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => new { e.TenantId, e.BranchId, e.CashierShiftId, e.Status })
                .HasFilter("\"CashierShiftId\" IS NOT NULL")
                .HasDatabaseName("IX_ExpenseInvoices_Tenant_Branch_Shift_Status");

            // ── ExpenseCategory ──────────────────────────────────────────────────
            // Unique name per tenant (filtered to non-deleted rows so a name can
            // be reused after a soft-delete).
            modelBuilder.Entity<ExpenseCategory>()
                .HasIndex(c => new { c.TenantId, c.Name })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_ExpenseCategories_Tenant_Name");

            // Drives the picker dropdown order.
            modelBuilder.Entity<ExpenseCategory>()
                .HasIndex(c => new { c.TenantId, c.IsActive, c.SortOrder })
                .HasDatabaseName("IX_ExpenseCategories_Tenant_Active_Sort");

            // Cascade attachment rows when the parent invoice is deleted.
            // (Soft-delete on the parent is handled by the global filter; physical
            // file cleanup is the service layer's responsibility.)
            modelBuilder.Entity<ExpenseInvoice>()
                .HasMany(e => e.Attachments)
                .WithOne(a => a.ExpenseInvoice)
                .HasForeignKey(a => a.ExpenseInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExpenseInvoice>()
                .HasMany(e => e.AuditLogs)
                .WithOne(a => a.ExpenseInvoice)
                .HasForeignKey(a => a.ExpenseInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Drives the default list query: tenant + date-desc.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => new { e.TenantId, e.InvoiceDate })
                .HasDatabaseName("IX_ExpenseInvoices_Tenant_Date");

            // Status filter on the list page (Unpaid / Paid / etc.) over date.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => new { e.TenantId, e.Status, e.InvoiceDate })
                .HasDatabaseName("IX_ExpenseInvoices_Tenant_Status_Date");

            // Supplier filter — fast lookup by FK once the Suppliers module lands.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => new { e.TenantId, e.SupplierId })
                .HasDatabaseName("IX_ExpenseInvoices_Tenant_Supplier");

            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => new { e.TenantId, e.ExpenseCategoryId })
                .HasDatabaseName("IX_ExpenseInvoices_Tenant_Category");

            // Invoice numbers are supplier-owned references, not system identity.
            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => new { e.TenantId, e.InvoiceNumber })
                .HasDatabaseName("IX_ExpenseInvoices_Tenant_InvoiceNumber");

            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => new { e.TenantId, e.SupplierId, e.InvoiceNumber, e.InvoiceDate })
                .HasFilter("\"SupplierId\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_ExpenseInvoices_Duplicate_RegisteredSupplier");

            modelBuilder.Entity<ExpenseInvoice>()
                .HasIndex(e => new { e.TenantId, e.SupplierName, e.InvoiceNumber, e.InvoiceDate })
                .HasFilter("\"SupplierId\" IS NULL AND \"SupplierName\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_ExpenseInvoices_Duplicate_ManualSupplier");

            modelBuilder.Entity<ExpenseInvoiceAuditLog>()
                .HasIndex(a => new { a.TenantId, a.ExpenseInvoiceId, a.AcknowledgedAt })
                .HasDatabaseName("IX_ExpenseInvoiceAuditLogs_Tenant_Invoice_Date");

            // ── ExpenseInvoiceAttachment ──────────────────────────────────────────
            modelBuilder.Entity<ExpenseInvoiceAttachment>()
                .HasOne(a => a.UploadedByUser)
                .WithMany()
                .HasForeignKey(a => a.UploadedById)
                .OnDelete(DeleteBehavior.SetNull);

            // List-time "attachments count" + "primary thumb" lookup.
            modelBuilder.Entity<ExpenseInvoiceAttachment>()
                .HasIndex(a => new { a.TenantId, a.ExpenseInvoiceId })
                .HasDatabaseName("IX_ExpenseInvoiceAttachments_Tenant_Invoice");

            // ── Assets Management ────────────────────────────────────────────────
            modelBuilder.Entity<AssetCategory>()
                .HasIndex(c => new { c.TenantId, c.NameEn })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_AssetCategories_Tenant_NameEn");

            modelBuilder.Entity<AssetCategory>()
                .HasIndex(c => new { c.TenantId, c.IsActive, c.SortOrder })
                .HasDatabaseName("IX_AssetCategories_Tenant_Active_Sort");

            modelBuilder.Entity<Asset>()
                .HasOne(a => a.AssetCategory)
                .WithMany(c => c.Assets)
                .HasForeignKey(a => a.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Asset>()
                .HasOne(a => a.Branch)
                .WithMany()
                .HasForeignKey(a => a.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Asset>()
                .HasIndex(a => new { a.TenantId, a.BranchId, a.Status })
                .HasDatabaseName("IX_Assets_Tenant_Branch_Status");

            modelBuilder.Entity<Asset>()
                .HasOne(a => a.AssignedToEmployee)
                .WithMany()
                .HasForeignKey(a => a.AssignedToEmployeeId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Asset>()
                .HasIndex(a => new { a.TenantId, a.AssetCode })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Assets_Tenant_AssetCode");

            modelBuilder.Entity<Asset>()
                .HasIndex(a => new { a.TenantId, a.AssetCategoryId, a.Status })
                .HasDatabaseName("IX_Assets_Tenant_Category_Status");

            modelBuilder.Entity<Asset>()
                .HasIndex(a => new { a.TenantId, a.Status, a.Condition })
                .HasDatabaseName("IX_Assets_Tenant_Status_Condition");

            modelBuilder.Entity<Asset>()
                .HasIndex(a => new { a.TenantId, a.AssignedToEmployeeId })
                .HasDatabaseName("IX_Assets_Tenant_AssignedEmployee");

            modelBuilder.Entity<Asset>()
                .HasIndex(a => new { a.TenantId, a.WarrantyEndDate })
                .HasDatabaseName("IX_Assets_Tenant_WarrantyEnd");

            modelBuilder.Entity<Asset>()
                .HasIndex(a => new { a.TenantId, a.SerialNumber })
                .HasFilter("\"SerialNumber\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Assets_Tenant_SerialNumber");

            modelBuilder.Entity<Asset>()
                .HasIndex(a => new { a.TenantId, a.Barcode })
                .HasFilter("\"Barcode\" IS NOT NULL AND \"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_Assets_Tenant_Barcode");

            modelBuilder.Entity<Asset>()
                .Property(a => a.PurchaseCost)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Asset>()
                .Property(a => a.CurrentValue)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<AssetAttachment>()
                .HasOne(a => a.Asset)
                .WithMany(a => a.Attachments)
                .HasForeignKey(a => a.AssetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssetAttachment>()
                .HasOne(a => a.MaintenanceRecord)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.MaintenanceRecordId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AssetAttachment>()
                .HasIndex(a => new { a.TenantId, a.AssetId, a.UploadedAt })
                .HasDatabaseName("IX_AssetAttachments_Tenant_Asset_Uploaded");

            modelBuilder.Entity<AssetAttachment>()
                .HasIndex(a => new { a.TenantId, a.MaintenanceRecordId })
                .HasDatabaseName("IX_AssetAttachments_Tenant_Maintenance");

            modelBuilder.Entity<AssetMaintenanceRecord>()
                .HasOne(m => m.Asset)
                .WithMany(a => a.MaintenanceRecords)
                .HasForeignKey(m => m.AssetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssetMaintenanceRecord>()
                .Property(m => m.Cost)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<AssetMaintenanceRecord>()
                .HasIndex(m => new { m.TenantId, m.AssetId, m.MaintenanceDate })
                .HasDatabaseName("IX_AssetMaintenance_Tenant_Asset_Date");

            modelBuilder.Entity<AssetMaintenanceRecord>()
                .HasIndex(m => new { m.TenantId, m.Status, m.NextMaintenanceDate })
                .HasDatabaseName("IX_AssetMaintenance_Tenant_Status_Next");

            modelBuilder.Entity<AssetActivityLog>()
                .HasOne(l => l.Asset)
                .WithMany(a => a.ActivityLogs)
                .HasForeignKey(l => l.AssetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssetActivityLog>()
                .HasIndex(l => new { l.TenantId, l.AssetId, l.Timestamp })
                .HasDatabaseName("IX_AssetActivityLogs_Tenant_Asset_Time");

            modelBuilder.Entity<AssetActivityLog>()
                .HasIndex(l => new { l.TenantId, l.ActionType, l.Timestamp })
                .HasDatabaseName("IX_AssetActivityLogs_Tenant_Action_Time");

            // ── Multi-printer routing (ProductKitchenPrinter junction) ────────────
            modelBuilder.Entity<ProductKitchenPrinter>(b =>
            {
                b.HasOne(x => x.Product)
                    .WithMany(p => p.KitchenPrinters)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.KitchenPrinter)
                    .WithMany()
                    .HasForeignKey(x => x.KitchenPrinterId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => new { x.TenantId, x.ProductId })
                    .HasDatabaseName("IX_ProductKitchenPrinters_Tenant_Product");

                b.HasIndex(x => new { x.TenantId, x.ProductId, x.KitchenPrinterId })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_ProductKitchenPrinters_Tenant_Product_Printer");
            });

            // ── Storefront product gallery (additive; primary image stays on Product) ──
            modelBuilder.Entity<ProductImage>(b =>
            {
                b.HasOne(x => x.Product)
                    .WithMany(p => p.Images)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(x => new { x.TenantId, x.ProductId, x.SortOrder })
                    .HasDatabaseName("IX_ProductImages_Tenant_Product_Sort");
            });

            // ── Storefront promotional banners (additive; cover image stays the fallback) ──
            modelBuilder.Entity<StorefrontBanner>(b =>
            {
                b.Property(x => x.LinkType).HasConversion<int>();
                b.Property(x => x.Placement).HasConversion<int>();

                b.HasIndex(x => new { x.TenantId, x.Placement, x.IsActive, x.SortOrder })
                    .HasDatabaseName("IX_StorefrontBanners_Tenant_Placement_Active_Sort");
            });

            // ── Brand identity (logo master for the catalogue's free-text brand) ──
            modelBuilder.Entity<ProductBrand>(b =>
            {
                b.HasIndex(x => new { x.TenantId, x.NormalizedName })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_ProductBrands_Tenant_NormalizedName");

                b.HasIndex(x => new { x.TenantId, x.IsActive, x.SortOrder })
                    .HasDatabaseName("IX_ProductBrands_Tenant_Active_Sort");
            });

            // ── Currency lookup (selection metadata only — no FK, no amounts) ────
            modelBuilder.Entity<Currency>(b =>
            {
                b.HasIndex(x => new { x.TenantId, x.Code })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_Currencies_Tenant_Code");

                b.HasIndex(x => new { x.TenantId, x.IsActive, x.SortOrder })
                    .HasDatabaseName("IX_Currencies_Tenant_Active_Sort");
            });

            // ── WhatsApp destinations for the storefront ──────────────────────────
            modelBuilder.Entity<WhatsAppContact>(b =>
            {
                b.HasIndex(x => new { x.TenantId, x.Purpose, x.IsActive, x.SortOrder })
                    .HasDatabaseName("IX_WhatsAppContacts_Tenant_Purpose_Active_Sort");
            });

            // ── Multi-warehouse inventory ─────────────────────────────────────────
            modelBuilder.Entity<Warehouse>(b =>
            {
                b.HasIndex(w => new { w.TenantId, w.Code })
                    .IsUnique()
                    .HasFilter("\"DeletedAt\" IS NULL")
                    .HasDatabaseName("IX_Warehouses_Tenant_Code");

                b.HasIndex(w => new { w.TenantId, w.Type, w.IsActive })
                    .HasDatabaseName("IX_Warehouses_Tenant_Type_Active");
            });

            modelBuilder.Entity<RawMaterialInventory>(b =>
            {
                b.HasOne(i => i.RawMaterial)
                    .WithMany()
                    .HasForeignKey(i => i.RawMaterialId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(i => i.Warehouse)
                    .WithMany(w => w.Inventories)
                    .HasForeignKey(i => i.WarehouseId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(i => new { i.TenantId, i.BranchId, i.WarehouseId })
                    .HasDatabaseName("IX_RawMaterialInventories_Tenant_Branch_Warehouse");
            });

            modelBuilder.Entity<InventoryTransfer>(b =>
            {
                b.HasOne(t => t.FromWarehouse)
                    .WithMany()
                    .HasForeignKey(t => t.FromWarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(t => t.ToWarehouse)
                    .WithMany()
                    .HasForeignKey(t => t.ToWarehouseId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(t => t.RawMaterial)
                    .WithMany()
                    .HasForeignKey(t => t.RawMaterialId)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(t => t.PerformedBy)
                    .WithMany()
                    .HasForeignKey(t => t.PerformedById)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(t => new { t.TenantId, t.CreatedAt })
                    .HasDatabaseName("IX_InventoryTransfers_Tenant_CreatedAt");

                b.HasIndex(t => new { t.TenantId, t.RawMaterialId, t.CreatedAt })
                    .HasDatabaseName("IX_InventoryTransfers_Tenant_Material_CreatedAt");
            });

            modelBuilder.Entity<RawMaterial>(b =>
            {
                b.HasOne(m => m.DefaultWarehouse)
                    .WithMany()
                    .HasForeignKey(m => m.DefaultWarehouseId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(m => new { m.TenantId, m.DefaultWarehouseId })
                    .HasDatabaseName("IX_RawMaterials_Tenant_DefaultWarehouse");
            });

            modelBuilder.Entity<Branch>().HasData(SeedData.DefaultBranches);
            modelBuilder.Entity<User>().HasData(SeedData.DefaultUsers);
            modelBuilder.Entity<PaymentMethod>().HasData(SeedData.DefaultPaymentMethods);
            // The exact set the back office could already choose from before this lookup
            // existed, so nothing the business can select today stops being selectable.
            modelBuilder.Entity<Currency>().HasData(SeedData.DefaultCurrencies);

            // ── Seed default CancelReasons (global — TenantId = null) ─────────────
            modelBuilder.Entity<CancelReason>().HasData(
                new CancelReason { Id = 1, Code = "CLIENT_CHANGED_MIND", Name = "Client changed mind",   NameAr = "العميل غير رأيه",      RequiresNote = false, SortOrder = 1 },
                new CancelReason { Id = 2, Code = "WRONG_ORDER_ENTERED", Name = "Wrong order entered",   NameAr = "تم إدخال طلب خاطئ",    RequiresNote = false, SortOrder = 2 },
                new CancelReason { Id = 3, Code = "CLIENT_LEFT",         Name = "Client left",           NameAr = "العميل غادر",           RequiresNote = false, SortOrder = 3 },
                new CancelReason { Id = 4, Code = "ALLERGY_CONCERN",     Name = "Allergy concern",       NameAr = "مشكلة حساسية",          RequiresNote = false, SortOrder = 4 },
                new CancelReason { Id = 5, Code = "ITEM_UNAVAILABLE",    Name = "Item unavailable",      NameAr = "المنتج غير متوفر",      RequiresNote = false, SortOrder = 5 },
                new CancelReason { Id = 6, Code = "OTHER",               Name = "Other",                 NameAr = "سبب آخر",               RequiresNote = true,  SortOrder = 6 }
            );

            // Marketing Engine schema (isolated module). Single integration line.
            modelBuilder.ApplyMarketingEngineConfigurations();

            // Payment Engine schema (provider-agnostic foundation). Single integration line.
            modelBuilder.ApplyPaymentEngineConfigurations();

            // Retail module schema (isolated module). Single integration line.
            modelBuilder.ApplyRetailConfigurations();
        }

        // Auto-stamp CreatedAt on insert and UpdatedAt on insert/update for every BaseEntity.
        // CreatedAt is locked once written (IsModified = false on Modified entries).
        public override int SaveChanges()
        {
            StampAuditColumns();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            StampAuditColumns();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            StampAuditColumns();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            StampAuditColumns();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void StampAuditColumns()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                }
            }
        }
    }
}
