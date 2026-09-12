using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Data.Configurations
{
    public class LoyaltyTierConfiguration : IEntityTypeConfiguration<LoyaltyTier>
    {
        public void Configure(EntityTypeBuilder<LoyaltyTier> b)
        {
            b.ToTable("LoyaltyTiers");

            // Each legacy enum value maps to at most one active tier (keeps Customer.Tier sync deterministic).
            b.HasIndex(t => new { t.TenantId, t.LegacyTierEnum })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL");
            b.HasIndex(t => new { t.TenantId, t.SortOrder })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL");
            b.HasIndex(t => new { t.TenantId, t.BranchId, t.IsActive, t.MinPoints });
        }
    }

    public class TierBenefitConfiguration : IEntityTypeConfiguration<TierBenefit>
    {
        public void Configure(EntityTypeBuilder<TierBenefit> b)
        {
            b.ToTable("TierBenefits");
            b.HasIndex(x => x.TierId);

            b.HasOne(x => x.Tier)
                .WithMany(t => t.Benefits)
                .HasForeignKey(x => x.TierId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class CustomerTierHistoryConfiguration : IEntityTypeConfiguration<CustomerTierHistory>
    {
        public void Configure(EntityTypeBuilder<CustomerTierHistory> b)
        {
            b.ToTable("CustomerTierHistories");

            b.HasIndex(h => new { h.TenantId, h.CustomerId, h.ChangedAt });
            b.HasIndex(h => new { h.TenantId, h.ChangedAt });

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(h => h.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<LoyaltyTier>()
                .WithMany()
                .HasForeignKey(h => h.NewTierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
