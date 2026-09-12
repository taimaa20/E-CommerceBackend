using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Data.Configurations
{
    public class EarningRuleConfiguration : IEntityTypeConfiguration<EarningRule>
    {
        public void Configure(EntityTypeBuilder<EarningRule> b)
        {
            b.ToTable("EarningRules");

            b.HasIndex(r => new { r.TenantId, r.RuleGroupId, r.RuleVersion }).IsUnique();

            // Exactly one current version per rule group (active rows only).
            b.HasIndex(r => new { r.TenantId, r.RuleGroupId })
                .IsUnique()
                .HasFilter("\"IsCurrent\" = true AND \"DeletedAt\" IS NULL");

            b.HasIndex(r => new { r.TenantId, r.BranchId, r.IsActive, r.IsCurrent });
            b.HasIndex(r => r.CampaignId);

            b.HasOne<Category>()
                .WithMany()
                .HasForeignKey(r => r.TargetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Product>()
                .WithMany()
                .HasForeignKey(r => r.TargetProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // CampaignId has no FK yet — Campaign entity ships in Phase 4.
        }
    }

    public class MarketingSettingsConfiguration : IEntityTypeConfiguration<MarketingSettings>
    {
        public void Configure(EntityTypeBuilder<MarketingSettings> b)
        {
            b.ToTable("MarketingSettings");

            // One settings row per tenant.
            b.HasIndex(s => s.TenantId).IsUnique();
        }
    }
}
