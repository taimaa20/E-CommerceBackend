using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class CostProfitabilityRepository : ICostProfitabilityRepository
    {
        private readonly PosDbContext _context;

        public CostProfitabilityRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<List<CostSharingProvider>> GetProvidersAsync(
            CostProviderType? type,
            bool includeInactive,
            CancellationToken ct)
        {
            var query = _context.CostSharingProviders.AsNoTracking();
            if (type.HasValue) query = query.Where(p => p.Type == type.Value);
            if (!includeInactive) query = query.Where(p => p.IsActive);
            return query.OrderBy(p => p.Type).ThenBy(p => p.Name).ToListAsync(ct);
        }

        public Task<CostSharingProvider?> GetProviderByIdAsync(Guid id, CancellationToken ct)
            => _context.CostSharingProviders.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task<bool> ProviderCodeExistsAsync(string code, Guid? excludeId, CancellationToken ct)
        {
            var normalized = code.Trim().ToUpperInvariant();
            return await _context.CostSharingProviders.AsNoTracking()
                .AnyAsync(p => p.Code.ToUpper() == normalized
                    && (!excludeId.HasValue || p.Id != excludeId.Value), ct);
        }

        public async Task<CostSharingProvider> AddProviderAsync(CostSharingProvider provider, CancellationToken ct)
        {
            _context.CostSharingProviders.Add(provider);
            await _context.SaveChangesAsync(ct);
            return provider;
        }

        public async Task<CostSharingProvider> UpdateProviderAsync(CostSharingProvider provider, CancellationToken ct)
        {
            _context.CostSharingProviders.Update(provider);
            await _context.SaveChangesAsync(ct);
            return provider;
        }

        public async Task DeleteProviderAsync(CostSharingProvider provider, CancellationToken ct)
        {
            provider.IsActive = false;
            provider.DeletedAt = DateTime.UtcNow;
            _context.CostSharingProviders.Update(provider);
            await _context.SaveChangesAsync(ct);
        }

        public Task<List<CostSharingRule>> GetRulesAsync(
            CostProviderType? providerType,
            bool includeInactive,
            CancellationToken ct)
        {
            IQueryable<CostSharingRule> query = _context.CostSharingRules.AsNoTracking()
                .Include(r => r.Provider);
            if (providerType.HasValue) query = query.Where(r => r.ProviderType == providerType.Value);
            if (!includeInactive) query = query.Where(r => r.IsActive);
            return query.OrderByDescending(r => r.Priority).ThenBy(r => r.Scope).ToListAsync(ct);
        }

        public Task<CostSharingRule?> GetRuleByIdAsync(Guid id, CancellationToken ct)
            => _context.CostSharingRules.AsNoTracking()
                .Include(r => r.Provider)
                .FirstOrDefaultAsync(r => r.Id == id, ct);

        public async Task<CostSharingRule> AddRuleAsync(CostSharingRule rule, CancellationToken ct)
        {
            _context.CostSharingRules.Add(rule);
            await _context.SaveChangesAsync(ct);
            return rule;
        }

        public async Task<CostSharingRule> UpdateRuleAsync(CostSharingRule rule, CancellationToken ct)
        {
            _context.CostSharingRules.Update(rule);
            await _context.SaveChangesAsync(ct);
            return rule;
        }

        public async Task DeleteRuleAsync(CostSharingRule rule, CancellationToken ct)
        {
            rule.IsActive = false;
            rule.DeletedAt = DateTime.UtcNow;
            _context.CostSharingRules.Update(rule);
            await _context.SaveChangesAsync(ct);
        }

        public Task<Order?> GetOrderForSnapshotAsync(Guid orderId, CancellationToken ct)
        {
            return _context.Orders
                .Include(o => o.PaidByUser)
                .Include(o => o.Customer)
                .Include(o => o.DeliveryPartner)
                .Include(o => o.Payments)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(i => i.Modifiers)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        }

        public Task<OrderProfitabilitySnapshot?> GetSnapshotForUpdateAsync(Guid orderId, CancellationToken ct)
            => _context.OrderProfitabilitySnapshots
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

        public Task<OrderProfitabilitySnapshot?> GetSnapshotByOrderIdAsync(Guid orderId, CancellationToken ct)
            => _context.OrderProfitabilitySnapshots.AsNoTracking()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

        public void AddSnapshot(OrderProfitabilitySnapshot snapshot)
            => _context.OrderProfitabilitySnapshots.Add(snapshot);

        public Task SaveChangesAsync(CancellationToken ct)
            => _context.SaveChangesAsync(ct);

        public async Task<CostProfitabilitySummaryDto> GetSummaryAsync(
            CostProfitabilityFilterDto filter,
            CancellationToken ct)
        {
            var row = await ApplySnapshotFilter(_context.OrderProfitabilitySnapshots.AsNoTracking(), filter)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Sales = g.Sum(s => s.GrossSales),
                    NetSales = g.Sum(s => s.NetSales),
                    FoodCost = g.Sum(s => s.FoodCost),
                    DeliveryCost = g.Sum(s => s.DeliveryCost),
                    PartnerFees = g.Sum(s => s.PartnerCommission),
                    CardFees = g.Sum(s => s.CardFees),
                    RestaurantShare = g.Sum(s => s.RestaurantCostShare),
                    ProviderShare = g.Sum(s => s.ProviderCostShare),
                    GrossProfit = g.Sum(s => s.GrossProfit),
                    NetProfit = g.Sum(s => s.NetProfit),
                    Commission = g.Sum(s => s.ProviderCommission),
                    TransactionCost = g.Sum(s => s.TotalRestaurantFees)
                })
                .FirstOrDefaultAsync(ct);

            return BuildSummary(row);
        }

        public async Task<List<CostPartnerAnalyticsDto>> GetPartnerAnalyticsAsync(
            CostProfitabilityFilterDto filter,
            CancellationToken ct)
        {
            var rows = await ApplySnapshotFilter(_context.OrderProfitabilitySnapshots.AsNoTracking(), filter)
                .Where(s => s.DeliveryPartnerId.HasValue || s.OrderSource == OrderSource.Talabat)
                .GroupBy(s => new { s.DeliveryPartnerId, s.DeliveryPartnerName, s.DeliveryPartnerNameAr, s.DeliveryPartnerCode })
                .Select(g => new CostPartnerAnalyticsDto
                {
                    PartnerId = g.Key.DeliveryPartnerId,
                    Name = g.Key.DeliveryPartnerName ?? "Talabat",
                    NameAr = g.Key.DeliveryPartnerNameAr,
                    Code = g.Key.DeliveryPartnerCode,
                    OrderCount = g.Count(),
                    GrossSales = g.Sum(s => s.GrossSales),
                    NetSales = g.Sum(s => s.NetSales),
                    CommissionAmount = g.Sum(s => s.PartnerCommission),
                    RestaurantShare = g.Sum(s => s.RestaurantCostShare),
                    PartnerShare = g.Sum(s => s.ProviderCostShare),
                    TotalCost = g.Sum(s => s.FoodCost + s.DeliveryCost + s.RestaurantCostShare),
                    GrossProfit = g.Sum(s => s.GrossProfit),
                    NetProfit = g.Sum(s => s.NetProfit)
                })
                .OrderByDescending(r => r.NetProfit)
                .ToListAsync(ct);

            return rows.Select(FinalizePartnerRow).ToList();
        }

        public async Task<List<CostPaymentMethodAnalyticsDto>> GetPaymentMethodAnalyticsAsync(
            CostProfitabilityFilterDto filter,
            CancellationToken ct)
        {
            var rows = await ApplySnapshotFilter(_context.OrderProfitabilitySnapshots.AsNoTracking(), filter)
                .GroupBy(s => new { s.PaymentMethodId, s.PaymentMethodName, s.PaymentMethodNameAr, s.PaymentMethodCode })
                .Select(g => new CostPaymentMethodAnalyticsDto
                {
                    PaymentMethodId = g.Key.PaymentMethodId,
                    Name = g.Key.PaymentMethodName ?? "Legacy payment",
                    NameAr = g.Key.PaymentMethodNameAr,
                    Code = g.Key.PaymentMethodCode,
                    Transactions = g.Count(),
                    Sales = g.Sum(s => s.GrossSales),
                    Fees = g.Sum(s => s.CardFees),
                    RestaurantShare = g.Sum(s => s.PaymentProcessingFee),
                    ProviderShare = g.Sum(s => s.ProviderCostShare),
                    NetRevenue = g.Sum(s => s.NetRevenue),
                    GrossProfit = g.Sum(s => s.GrossProfit),
                    NetProfit = g.Sum(s => s.NetProfit)
                })
                .OrderByDescending(r => r.Fees)
                .ToListAsync(ct);

            return rows.Select(FinalizePaymentRow).ToList();
        }

        public async Task<List<CostProductProfitabilityDto>> GetProductProfitabilityAsync(
            CostProfitabilityFilterDto filter,
            CancellationToken ct)
        {
            var snapshots = ApplySnapshotFilter(_context.OrderProfitabilitySnapshots.AsNoTracking(), filter);
            var rows = await _context.OrderProfitabilitySnapshotItems.AsNoTracking()
                .Where(i => snapshots.Any(s => s.Id == i.SnapshotId))
                .GroupBy(i => new { i.ProductId, i.ProductName, i.ProductNameAr })
                .Select(g => new CostProductProfitabilityDto
                {
                    ProductId = g.Key.ProductId,
                    Name = g.Key.ProductName,
                    NameAr = g.Key.ProductNameAr,
                    Quantity = g.Sum(i => i.Quantity),
                    Revenue = g.Sum(i => i.GrossRevenue),
                    FoodCost = g.Sum(i => i.FoodCost),
                    CommissionCost = g.Sum(i => i.CommissionCost),
                    CardFeeCost = g.Sum(i => i.CardFeeCost),
                    OperationalCost = g.Sum(i => i.OperationalCost),
                    GrossProfit = g.Sum(i => i.GrossProfit),
                    NetProfit = g.Sum(i => i.NetProfit)
                })
                .OrderByDescending(r => r.NetProfit)
                .Take(25)
                .ToListAsync(ct);

            return rows.Select(FinalizeProductRow).ToList();
        }

        public async Task<List<CostCustomerProfitabilityDto>> GetCustomerProfitabilityAsync(
            CostProfitabilityFilterDto filter,
            CancellationToken ct)
        {
            var rows = await ApplySnapshotFilter(_context.OrderProfitabilitySnapshots.AsNoTracking(), filter)
                .GroupBy(s => new { s.CustomerId, s.CustomerName })
                .Select(g => new CostCustomerProfitabilityDto
                {
                    CustomerId = g.Key.CustomerId,
                    Name = g.Key.CustomerName ?? "Anonymous",
                    Visits = g.Count(),
                    LifetimeRevenue = g.Sum(s => s.NetRevenue),
                    LifetimeCost = g.Sum(s => s.FoodCost + s.DeliveryCost + s.RestaurantCostShare),
                    LifetimeProfit = g.Sum(s => s.NetProfit)
                })
                .OrderByDescending(r => r.LifetimeProfit)
                .Take(25)
                .ToListAsync(ct);

            return rows.Select(FinalizeCustomerRow).ToList();
        }

        private IQueryable<OrderProfitabilitySnapshot> ApplySnapshotFilter(
            IQueryable<OrderProfitabilitySnapshot> query,
            CostProfitabilityFilterDto filter)
        {
            if (filter.DateFrom.HasValue) query = query.Where(s => (s.PaidAt ?? s.SnapshotAt) >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue) query = query.Where(s => (s.PaidAt ?? s.SnapshotAt) < filter.DateTo.Value);
            if (filter.PartnerId.HasValue) query = query.Where(s => s.DeliveryPartnerId == filter.PartnerId);
            if (filter.PaymentMethodId.HasValue) query = query.Where(s => s.PaymentMethodId == filter.PaymentMethodId);
            if (filter.CashierId.HasValue) query = query.Where(s => s.CashierId == filter.CashierId);
            if (filter.CustomerId.HasValue) query = query.Where(s => s.CustomerId == filter.CustomerId);
            if (filter.OrderType.HasValue) query = query.Where(s => s.OrderType == filter.OrderType);
            if (filter.OrderSource.HasValue) query = query.Where(s => s.OrderSource == filter.OrderSource);
            if (filter.OrderStatus.HasValue) query = query.Where(s => s.OrderStatus == filter.OrderStatus);
            return ApplyProviderFilter(query, filter.ProviderId);
        }

        private IQueryable<OrderProfitabilitySnapshot> ApplyProviderFilter(
            IQueryable<OrderProfitabilitySnapshot> query,
            Guid? providerId)
        {
            if (!providerId.HasValue) return query;
            return query.Where(s => _context.CostSharingProviders.Any(p =>
                p.Id == providerId.Value
                && (p.ExternalReferenceId == s.DeliveryPartnerId
                    || p.ExternalReferenceId == s.PaymentMethodId)));
        }

        private static CostProfitabilitySummaryDto BuildSummary(dynamic? row)
        {
            if (row == null) return new CostProfitabilitySummaryDto();
            decimal netSales = Round(row.NetSales);
            decimal sales = Round(row.Sales);
            int count = row.Count;

            return new CostProfitabilitySummaryDto
            {
                OrderCount = count,
                TotalSales = sales,
                TotalNetSales = netSales,
                TotalFoodCost = Round(row.FoodCost),
                TotalDeliveryCost = Round(row.DeliveryCost),
                TotalPartnerFees = Round(row.PartnerFees),
                TotalCardFees = Round(row.CardFees),
                TotalRestaurantCostShare = Round(row.RestaurantShare),
                TotalProviderCostShare = Round(row.ProviderShare),
                GrossProfit = Round(row.GrossProfit),
                NetProfit = Round(row.NetProfit),
                GrossMarginPercentage = Percent(row.GrossProfit, netSales),
                NetMarginPercentage = Percent(row.NetProfit, netSales),
                AverageProfitPerOrder = count == 0 ? 0 : Round(row.NetProfit / count),
                AverageCommissionPerOrder = count == 0 ? 0 : Round(row.Commission / count),
                AverageFeePercentage = Percent(row.Commission, sales),
                AverageTransactionCost = count == 0 ? 0 : Round(row.TransactionCost / count)
            };
        }

        private static CostPartnerAnalyticsDto FinalizePartnerRow(CostPartnerAnalyticsDto row)
        {
            row.GrossSales = Round(row.GrossSales);
            row.NetSales = Round(row.NetSales);
            row.AverageOrderValue = row.OrderCount == 0 ? 0 : Round(row.GrossSales / row.OrderCount);
            row.ProfitMargin = Percent(row.NetProfit, row.NetSales);
            row.TotalCost = Round(row.TotalCost);
            row.GrossProfit = Round(row.GrossProfit);
            row.NetProfit = Round(row.NetProfit);
            return row;
        }

        private static CostPaymentMethodAnalyticsDto FinalizePaymentRow(CostPaymentMethodAnalyticsDto row)
        {
            row.Sales = Round(row.Sales);
            row.Fees = Round(row.Fees);
            row.AverageFee = row.Transactions == 0 ? 0 : Round(row.Fees / row.Transactions);
            row.AverageCost = row.Transactions == 0 ? 0 : Round(row.RestaurantShare / row.Transactions);
            row.ProfitMargin = Percent(row.NetProfit, row.NetRevenue);
            row.NetProfit = Round(row.NetProfit);
            return row;
        }

        private static CostProductProfitabilityDto FinalizeProductRow(
            CostProductProfitabilityDto row,
            int index)
        {
            row.Ranking = index + 1;
            row.Margin = Percent(row.NetProfit, row.Revenue);
            row.NetProfit = Round(row.NetProfit);
            return row;
        }

        private static CostCustomerProfitabilityDto FinalizeCustomerRow(CostCustomerProfitabilityDto row)
        {
            row.AverageProfitPerVisit = row.Visits == 0 ? 0 : Round(row.LifetimeProfit / row.Visits);
            row.LifetimeProfit = Round(row.LifetimeProfit);
            return row;
        }

        private static decimal Percent(decimal value, decimal basis)
            => basis == 0 ? 0 : Round(value / basis * 100m);

        private static decimal Round(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
