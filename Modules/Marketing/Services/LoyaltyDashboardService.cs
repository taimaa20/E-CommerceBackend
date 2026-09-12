using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Services;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <summary>Read-only operational analytics for the loyalty program. All aggregation is DB-side.</summary>
    public interface ILoyaltyDashboardService
    {
        Task<LoyaltyDashboardDto> GetAsync(string? period, DateTime? from, DateTime? to, int trendDays, int topN, CancellationToken ct);
    }

    /// <inheritdoc cref="ILoyaltyDashboardService"/>
    public sealed class LoyaltyDashboardService : ILoyaltyDashboardService
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;
        private readonly ICacheService _cache;
        private readonly IConfiguration _configuration;

        public LoyaltyDashboardService(PosDbContext context, ITenantResolver tenant, ICacheService cache, IConfiguration configuration)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<LoyaltyDashboardDto> GetAsync(string? period, DateTime? from, DateTime? to, int trendDays, int topN, CancellationToken ct)
        {
            topN = topN is < 1 or > 50 ? 10 : topN;
            var tenantId = _tenant.GetTenantId();
            var window = DashboardDateRange.Resolve(period, from, to, DateTime.UtcNow);

            // Optional short-TTL cache (config-gated; 0/absent = disabled — no stale-critical-data by default).
            var cacheSeconds = _configuration.GetValue<int?>("Loyalty:Dashboard:CacheSeconds") ?? 0;
            if (cacheSeconds <= 0)
                return await BuildAsync(tenantId, window, topN, ct);

            var key = $"loyalty:dashboard:{tenantId}:{window.From:O}:{window.ToExclusive:O}:{topN}";
            return (await _cache.GetOrCreateAsync(key, () => BuildAsync(tenantId, window, topN, ct),
                absoluteExpiration: TimeSpan.FromSeconds(cacheSeconds), cancellationToken: ct))!;
        }

        private async Task<LoyaltyDashboardDto> BuildAsync(Guid tenantId, DashboardWindow window, int topN, CancellationToken ct)
        {
            // Tenant query filter scopes every set below to the current tenant automatically.
            var wallets = _context.CustomerWallets.AsNoTracking();
            var txns = _context.WalletTransactions.AsNoTracking();
            // Date-sensitive ledger rows are bounded by the selected window; current-state widgets
            // (totals, tiers, leaderboards) reflect "now" regardless of the window.
            var windowTxns = txns.Where(t => t.CreatedAt >= window.From && t.CreatedAt < window.ToExclusive);

            var totalMembers = await wallets.CountAsync(ct);
            var outstanding = await wallets.SumAsync(w => (decimal?)w.AvailablePoints, ct) ?? 0m;

            var kpis = new LoyaltyKpiDto
            {
                TotalMembers = totalMembers,
                ActiveMembers = await wallets.CountAsync(w => w.Status == WalletStatus.Active, ct),
                NewMembers = await wallets.CountAsync(w => w.CreatedAt >= window.From && w.CreatedAt < window.ToExclusive, ct),
                FrozenWallets = await wallets.CountAsync(w => w.Status == WalletStatus.Frozen, ct),
                TotalWalletBalance = outstanding,
                OutstandingPoints = outstanding,
                AveragePointsPerCustomer = totalMembers > 0 ? decimal.Round(outstanding / totalMembers, 2, MidpointRounding.AwayFromZero) : 0m,
                TotalPointsEarned = await windowTxns
                    .Where(t => t.Type == WalletTransactionType.Earn || t.Type == WalletTransactionType.EarnPendingApproved)
                    .SumAsync(t => (decimal?)t.Points, ct) ?? 0m,
                TotalPointsRedeemed = await windowTxns
                    .Where(t => t.Type == WalletTransactionType.Redeem)
                    .SumAsync(t => (decimal?)-t.Points, ct) ?? 0m,
                TotalPointsExpired = await windowTxns
                    .Where(t => t.Type == WalletTransactionType.Expire)
                    .SumAsync(t => (decimal?)-t.Points, ct) ?? 0m,
                AdjustmentCount = await windowTxns.CountAsync(t => t.Type == WalletTransactionType.Adjust, ct),
            };

            // Tier distribution from the legacy projection (always populated), names localized to EN.
            var tierCounts = await _context.Customers.AsNoTracking()
                .GroupBy(c => c.Tier)
                .Select(g => new { Tier = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var tiers = tierCounts
                .OrderBy(t => t.Tier)
                .Select(t => new TierCountDto { Tier = t.Tier.ToString(), Count = t.Count })
                .ToList();

            var earnerRows = await wallets.OrderByDescending(w => w.LifetimePoints).Take(topN)
                .Select(w => new { w.CustomerId, Value = w.LifetimePoints }).ToListAsync(ct);
            var redeemerRows = await wallets.OrderByDescending(w => w.RedeemedPoints).Take(topN)
                .Select(w => new { w.CustomerId, Value = w.RedeemedPoints }).ToListAsync(ct);
            var activeRows = await wallets.OrderByDescending(w => w.TotalOrders).Take(topN)
                .Select(w => new { w.CustomerId, Value = (decimal)w.TotalOrders }).ToListAsync(ct);

            var topEarners = await ProjectCustomersAsync(earnerRows.Select(x => (x.CustomerId, x.Value)).ToList(), ct);
            var topRedeemers = await ProjectCustomersAsync(redeemerRows.Select(x => (x.CustomerId, x.Value)).ToList(), ct);
            var mostActive = await ProjectCustomersAsync(activeRows.Select(x => (x.CustomerId, x.Value)).ToList(), ct);

            var trendRows = await windowTxns
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Earned = g.Where(t => t.Type == WalletTransactionType.Earn || t.Type == WalletTransactionType.EarnPendingApproved)
                        .Sum(t => (decimal?)t.Points) ?? 0m,
                    Redeemed = g.Where(t => t.Type == WalletTransactionType.Redeem).Sum(t => (decimal?)-t.Points) ?? 0m,
                    Expired = g.Where(t => t.Type == WalletTransactionType.Expire).Sum(t => (decimal?)-t.Points) ?? 0m,
                })
                .OrderBy(x => x.Date)
                .ToListAsync(ct);

            return new LoyaltyDashboardDto
            {
                Kpis = kpis,
                Tiers = tiers,
                TopEarners = topEarners,
                TopRedeemers = topRedeemers,
                MostActive = mostActive,
                Trends = trendRows
                    .Select(x => new LoyaltyTrendPointDto { Date = x.Date, Earned = x.Earned, Redeemed = x.Redeemed, Expired = x.Expired })
                    .ToList()
            };
        }

        private async Task<List<TopCustomerDto>> ProjectCustomersAsync(
            List<(Guid CustomerId, decimal Value)> rows, CancellationToken ct)
        {
            var ids = rows.Select(r => r.CustomerId).ToList();
            var names = await _context.Customers.AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.PhoneNumber })
                .ToDictionaryAsync(c => c.Id, ct);

            return rows.Select(r => new TopCustomerDto
            {
                CustomerId = r.CustomerId,
                Name = names.TryGetValue(r.CustomerId, out var c) ? c.Name : "—",
                PhoneNumber = names.TryGetValue(r.CustomerId, out var c2) ? c2.PhoneNumber : null,
                Value = r.Value
            }).ToList();
        }
    }
}
