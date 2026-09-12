using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Data.Configurations
{
    public class RewardConfiguration : IEntityTypeConfiguration<Reward>
    {
        public void Configure(EntityTypeBuilder<Reward> b)
        {
            b.ToTable("Rewards");

            b.HasIndex(r => new { r.TenantId, r.IsActive });
            b.HasIndex(r => new { r.TenantId, r.StartDate, r.EndDate });

            b.HasOne<Product>()
                .WithMany()
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RewardRedemptionConfiguration : IEntityTypeConfiguration<RewardRedemption>
    {
        public void Configure(EntityTypeBuilder<RewardRedemption> b)
        {
            b.ToTable("RewardRedemptions");

            b.HasIndex(x => new { x.TenantId, x.CustomerId, x.RedeemedAt });
            b.HasIndex(x => new { x.TenantId, x.RewardId });

            b.HasOne(x => x.Reward)
                .WithMany()
                .HasForeignKey(x => x.RewardId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
