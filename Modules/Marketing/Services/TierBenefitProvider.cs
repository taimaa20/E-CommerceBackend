using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    public enum TierBenefitEnforcement
    {
        /// <summary>Honored by the loyalty core today.</summary>
        Enforced = 0,
        /// <summary>Configured and stored, but no feature consumes it yet (Phase 3 will).</summary>
        NotYetEnforced = 1
    }

    /// <summary>A tier benefit plus whether the core currently enforces it.</summary>
    public sealed record ResolvedTierBenefit(
        Guid Id,
        TierBenefitType BenefitType,
        decimal? Value,
        Guid? RefId,
        string? Description,
        string? DescriptionAr,
        TierBenefitEnforcement Enforcement);

    /// <summary>
    /// Single source of truth for tier benefits. Future features (rewards, free delivery, campaign
    /// discounts) MUST consume this rather than re-reading <see cref="TierBenefit"/> or re-deciding
    /// enforcement — so benefit logic lives in one place and Phase 3 needs no core refactor.
    /// </summary>
    public interface ITierBenefitProvider
    {
        Task<IReadOnlyList<ResolvedTierBenefit>> GetForTierAsync(Guid tierId, CancellationToken ct);
        Task<IReadOnlyList<ResolvedTierBenefit>> GetForCustomerAsync(Guid customerId, CancellationToken ct);
    }

    /// <summary>Pure classification of which benefit types the loyalty core enforces today.</summary>
    public static class TierBenefitPolicy
    {
        public static TierBenefitEnforcement Status(TierBenefitType type) => type switch
        {
            // Tier earn multiplier is applied by the earning engine; everything else is Phase 3.
            TierBenefitType.Multiplier => TierBenefitEnforcement.Enforced,
            _ => TierBenefitEnforcement.NotYetEnforced
        };

        public static bool IsEnforced(TierBenefitType type) => Status(type) == TierBenefitEnforcement.Enforced;
    }

    /// <inheritdoc cref="ITierBenefitProvider"/>
    public sealed class TierBenefitProvider : ITierBenefitProvider
    {
        private readonly PosDbContext _context;

        public TierBenefitProvider(PosDbContext context)
            => _context = context ?? throw new ArgumentNullException(nameof(context));

        public async Task<IReadOnlyList<ResolvedTierBenefit>> GetForTierAsync(Guid tierId, CancellationToken ct)
        {
            var benefits = await _context.TierBenefits.IgnoreQueryFilters().AsNoTracking()
                .Where(b => b.TierId == tierId)
                .ToListAsync(ct);
            return benefits.Select(Map).ToList();
        }

        public async Task<IReadOnlyList<ResolvedTierBenefit>> GetForCustomerAsync(Guid customerId, CancellationToken ct)
        {
            var wallet = await _context.CustomerWallets.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);

            Guid? tierId = wallet?.CurrentTierId;
            if (tierId is null)
            {
                // Fall back to the legacy tier projection → active tier mapping.
                var customer = await _context.Customers.IgnoreQueryFilters().AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == customerId, ct);
                if (customer is null) return Array.Empty<ResolvedTierBenefit>();

                tierId = await _context.LoyaltyTiers.IgnoreQueryFilters().AsNoTracking()
                    .Where(t => t.TenantId == customer.TenantId && t.IsActive && t.LegacyTierEnum == customer.Tier)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync(ct);
            }

            return tierId is null ? Array.Empty<ResolvedTierBenefit>() : await GetForTierAsync(tierId.Value, ct);
        }

        private static ResolvedTierBenefit Map(TierBenefit b) => new(
            b.Id, b.BenefitType, b.Value, b.RefId, b.Description, b.DescriptionAr,
            TierBenefitPolicy.Status(b.BenefitType));
    }
}
