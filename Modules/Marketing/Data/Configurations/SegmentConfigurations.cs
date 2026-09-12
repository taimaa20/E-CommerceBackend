using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Data.Configurations
{
    public class CustomerSegmentConfiguration : IEntityTypeConfiguration<CustomerSegment>
    {
        public void Configure(EntityTypeBuilder<CustomerSegment> b)
        {
            b.ToTable("CustomerSegments");

            b.HasIndex(s => new { s.TenantId, s.Name })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL");
            b.HasIndex(s => new { s.TenantId, s.BranchId, s.IsActive });

            b.HasMany(s => s.Rules)
                .WithOne(r => r.Segment!)
                .HasForeignKey(r => r.SegmentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(s => s.Members)
                .WithOne(m => m.Segment!)
                .HasForeignKey(m => m.SegmentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class SegmentRuleConfiguration : IEntityTypeConfiguration<SegmentRule>
    {
        public void Configure(EntityTypeBuilder<SegmentRule> b)
        {
            b.ToTable("SegmentRules");
            b.HasIndex(r => r.SegmentId);
        }
    }

    public class SegmentMemberConfiguration : IEntityTypeConfiguration<SegmentMember>
    {
        public void Configure(EntityTypeBuilder<SegmentMember> b)
        {
            b.ToTable("SegmentMembers");

            b.HasIndex(m => new { m.SegmentId, m.CustomerId }).IsUnique();
            b.HasIndex(m => m.CustomerId);

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(m => m.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
