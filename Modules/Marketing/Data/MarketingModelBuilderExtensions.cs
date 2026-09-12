using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Data.Configurations;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Data
{
    /// <summary>
    /// Single integration point for the Marketing schema. Keeps PosDbContext.OnModelCreating
    /// touched by exactly one line. All marketing Fluent configuration lives here / in the
    /// per-entity IEntityTypeConfiguration classes.
    /// </summary>
    public static class MarketingModelBuilderExtensions
    {
        public static void ApplyMarketingEngineConfigurations(this ModelBuilder modelBuilder)
        {
            // Wallet aggregate
            modelBuilder.ApplyConfiguration(new CustomerWalletConfiguration());
            modelBuilder.ApplyConfiguration(new WalletTransactionConfiguration());
            modelBuilder.ApplyConfiguration(new PointsLotConfiguration());

            // Rules & settings
            modelBuilder.ApplyConfiguration(new EarningRuleConfiguration());
            modelBuilder.ApplyConfiguration(new MarketingSettingsConfiguration());

            // Segments
            modelBuilder.ApplyConfiguration(new CustomerSegmentConfiguration());
            modelBuilder.ApplyConfiguration(new SegmentRuleConfiguration());
            modelBuilder.ApplyConfiguration(new SegmentMemberConfiguration());

            // Tiers
            modelBuilder.ApplyConfiguration(new LoyaltyTierConfiguration());
            modelBuilder.ApplyConfiguration(new TierBenefitConfiguration());
            modelBuilder.ApplyConfiguration(new CustomerTierHistoryConfiguration());

            // Campaigns
            modelBuilder.ApplyConfiguration(new CampaignConfiguration());
            modelBuilder.ApplyConfiguration(new CampaignCustomerConfiguration());

            // Rewards
            modelBuilder.ApplyConfiguration(new RewardConfiguration());
            modelBuilder.ApplyConfiguration(new RewardRedemptionConfiguration());

            // Audit & merge
            modelBuilder.ApplyConfiguration(new MarketingAuditLogConfiguration());
            modelBuilder.ApplyConfiguration(new CustomerMergeHistoryConfiguration());

            // Optimistic concurrency for every marketing entity via the PostgreSQL xmin
            // system column — no stored column is created.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(MarketingBaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(nameof(MarketingBaseEntity.RowVersion))
                        .HasColumnName("xmin")
                        .HasColumnType("xid")
                        .ValueGeneratedOnAddOrUpdate()
                        .IsConcurrencyToken();
                }
            }

            // The single additive change to an existing table: a nullable loyalty reference on
            // Customers, plus a filtered-unique index that is safe on the populated table because
            // the column is brand-new (all existing rows are NULL, and NULLs do not collide).
            modelBuilder.Entity<Customer>()
                .HasIndex(c => new { c.TenantId, c.LoyaltyCustomerCode })
                .IsUnique()
                .HasFilter("\"LoyaltyCustomerCode\" IS NOT NULL");
        }
    }
}
