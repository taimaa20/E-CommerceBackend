using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Data.Configurations
{
    public class MarketingAuditLogConfiguration : IEntityTypeConfiguration<MarketingAuditLog>
    {
        public void Configure(EntityTypeBuilder<MarketingAuditLog> b)
        {
            b.ToTable("MarketingAuditLogs");

            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");

            b.HasIndex(x => new { x.TenantId, x.CreatedAt });
            b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
            b.HasIndex(x => new { x.CustomerId, x.CreatedAt });
        }
    }

    public class CustomerMergeHistoryConfiguration : IEntityTypeConfiguration<CustomerMergeHistory>
    {
        public void Configure(EntityTypeBuilder<CustomerMergeHistory> b)
        {
            b.ToTable("CustomerMergeHistories");

            // A customer can be merged away at most once.
            b.HasIndex(x => x.MergedCustomerId).IsUnique();
            b.HasIndex(x => x.SurvivorCustomerId);
            b.HasIndex(x => new { x.TenantId, x.CreatedAt });

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.SurvivorCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(x => x.MergedCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
