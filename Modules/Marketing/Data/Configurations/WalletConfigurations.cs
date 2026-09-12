using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Data.Configurations
{
    public class CustomerWalletConfiguration : IEntityTypeConfiguration<CustomerWallet>
    {
        public void Configure(EntityTypeBuilder<CustomerWallet> b)
        {
            b.ToTable("CustomerWallets");

            // One wallet per customer per tenant.
            b.HasIndex(w => new { w.TenantId, w.CustomerId }).IsUnique();
            b.HasIndex(w => new { w.TenantId, w.BranchId });
            b.HasIndex(w => w.CurrentTierId);
            b.HasIndex(w => w.Status).HasFilter("\"Status\" <> 0");

            // Dashboard top-N leaderboards sort by these per tenant — back them to avoid full sorts.
            b.HasIndex(w => new { w.TenantId, w.LifetimePoints });
            b.HasIndex(w => new { w.TenantId, w.RedeemedPoints });
            b.HasIndex(w => new { w.TenantId, w.TotalOrders });

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(w => w.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<LoyaltyTier>()
                .WithMany()
                .HasForeignKey(w => w.CurrentTierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
    {
        public void Configure(EntityTypeBuilder<WalletTransaction> b)
        {
            b.ToTable("WalletTransactions");

            // Replay-safety: one transaction per (wallet, idempotency key).
            b.HasIndex(t => new { t.WalletId, t.IdempotencyKey }).IsUnique();
            b.HasIndex(t => new { t.WalletId, t.Status });
            b.HasIndex(t => t.OrderId).HasFilter("\"OrderId\" IS NOT NULL");
            b.HasIndex(t => new { t.CustomerId, t.CreatedAt });
            b.HasIndex(t => new { t.TenantId, t.CreatedAt });
            b.HasIndex(t => t.MergeHistoryId).HasFilter("\"MergeHistoryId\" IS NOT NULL");
            // Campaign dashboard groups campaign-sourced rows by CampaignId — back it (filtered).
            b.HasIndex(t => t.CampaignId).HasFilter("\"CampaignId\" IS NOT NULL");
            b.HasIndex(t => t.ExpiresAt)
                .HasFilter("\"Status\" = 1 AND \"ExpiresAt\" IS NOT NULL"); // Status 1 = Available

            b.HasOne<CustomerWallet>()
                .WithMany()
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<Order>()
                .WithMany()
                .HasForeignKey(t => t.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<EarningRule>()
                .WithMany()
                .HasForeignKey(t => t.RuleVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<WalletTransaction>()
                .WithMany()
                .HasForeignKey(t => t.ReversesTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<CustomerMergeHistory>()
                .WithMany()
                .HasForeignKey(t => t.MergeHistoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // CampaignId has no FK yet — Campaign entity ships in Phase 4.
        }
    }

    public class PointsLotConfiguration : IEntityTypeConfiguration<PointsLot>
    {
        public void Configure(EntityTypeBuilder<PointsLot> b)
        {
            b.ToTable("PointsLots");

            // One lot per earn transaction.
            b.HasIndex(l => l.SourceTransactionId).IsUnique();
            b.HasIndex(l => new { l.WalletId, l.Status, l.EarnedAt });
            b.HasIndex(l => l.ExpiresAt)
                .HasFilter("\"Status\" = 0 AND \"ExpiresAt\" IS NOT NULL"); // Status 0 = Active

            b.HasOne<CustomerWallet>()
                .WithMany()
                .HasForeignKey(l => l.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<WalletTransaction>()
                .WithMany()
                .HasForeignKey(l => l.SourceTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
