using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Data.Configurations
{
    public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
    {
        public void Configure(EntityTypeBuilder<Campaign> b)
        {
            b.ToTable("Campaigns");

            b.HasIndex(c => new { c.TenantId, c.Status });
            b.HasIndex(c => new { c.TenantId, c.StartDate, c.EndDate });

            b.HasOne<CustomerSegment>()
                .WithMany()
                .HasForeignKey(c => c.TargetSegmentId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<LoyaltyTier>()
                .WithMany()
                .HasForeignKey(c => c.TargetTierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class CampaignCustomerConfiguration : IEntityTypeConfiguration<CampaignCustomer>
    {
        public void Configure(EntityTypeBuilder<CampaignCustomer> b)
        {
            b.ToTable("CampaignCustomers");

            b.HasIndex(x => new { x.CampaignId, x.CustomerId }).IsUnique();

            b.HasOne(x => x.Campaign)
                .WithMany(c => c.TargetCustomers)
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
