using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Common;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Financial;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Home;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Predicates;
using RestaurantPos.Api.Modules.Dashboard.Queries;

namespace RestaurantPos.Api.Modules.Dashboard.Services.Financial
{
    /// <summary>
    /// Owner / finance metrics. Recorded sales retain paid orders that were
    /// later refunded; refund logs are then deducted once on ProcessedAt.
    /// Operational paid counters keep their existing current-status semantics.
    /// </summary>
    public sealed class FinancialAnalyticsService : IFinancialAnalyticsService
    {
        private readonly IMetricQueryBuilder _q;
        private readonly IDashboardFilterContext _ctx;
        private readonly PosDbContext _db;
        private readonly ILogger<FinancialAnalyticsService> _logger;
        private const string UnknownCustomerName = "Guest";
        private const string UnknownPartnerName = "Delivery Partner";
        private const string UnknownPaymentMethodName = "Payment method";

        public FinancialAnalyticsService(IMetricQueryBuilder q, IDashboardFilterContext ctx, PosDbContext db, ILogger<FinancialAnalyticsService> logger)
        {
            _q   = q   ?? throw new ArgumentNullException(nameof(q));
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _db  = db  ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<FinancialSnapshotDto> BuildSnapshotAsync(CancellationToken ct)
        {
            var revenue       = await SafeAsync.RunAsync(_logger, "fin.revenue",       () => ComputeRevenueAsync(_q, _ctx.Window, ct), new RevenueBreakdownDto());
            var profitability = await SafeAsync.RunAsync(_logger, "fin.profitability", () => ComputeProfitabilityAsync(_q, _ctx.Window, revenue, ct), new ProfitabilityDto());
            var delivery      = await SafeAsync.RunAsync(_logger, "fin.delivery",      () => ComputeDeliveryAsync(_q, ct), new DeliveryMetricsDto());
            var costSharing   = await SafeAsync.RunAsync(_logger, "fin.costSharing",   () => ComputeCostSharingAsync(_q, ct), new CostSharingMetricsDto());
            var dailySeries   = await SafeAsync.RunAsync(_logger, "fin.dailySeries",   () => ComputeDailySeriesAsync(_q, ct), (IReadOnlyList<TimeSeriesPointDto>)Array.Empty<TimeSeriesPointDto>());
            var advanced      = await SafeAsync.RunAsync(_logger, "fin.advanced",      () => ComputeAdvancedAnalyticsAsync(ct), AdvancedFinancialAnalytics.Empty);
            var channels      = BuildChannelSplit(advanced.PaymentMethods);
            var bestSellers   = await SafeAsync.RunAsync(_logger, "fin.bestSellers",   () => ComputeRankedProductsAsync(_q, descending: true, ct), (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var worstSellers  = await SafeAsync.RunAsync(_logger, "fin.worstSellers",  () => ComputeRankedProductsAsync(_q, descending: false, ct), (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var byCashier     = await SafeAsync.RunAsync(_logger, "fin.byCashier",     () => ComputeRevenueByCashierAsync(_q, ct), (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var discountsByUser = await SafeAsync.RunAsync(_logger, "fin.discountsByUser", () => ComputeDiscountsByUserAsync(_q, ct), (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var cancellations    = await SafeAsync.RunAsync(_logger, "fin.cancellations",    () => ComputeCancellationSummaryAsync(ct), new CancellationSummaryDto());
            var categoryRevenue  = await SafeAsync.RunAsync(_logger, "fin.categoryRevenue",  () => ComputeCategoryRevenueAsync(ct), (IReadOnlyList<CategoryRevenueDto>)Array.Empty<CategoryRevenueDto>());
            var customerRevenue  = await SafeAsync.RunAsync(_logger, "fin.customerRevenue",  () => ComputeCustomerRevenueAsync(ct), new CustomerRevenueDto());
            var foodCost         = await SafeAsync.RunAsync(_logger, "fin.foodCost",         () => ComputeFoodCostAsync(revenue, ct), new FoodCostDto());
            var purchasing       = await SafeAsync.RunAsync(_logger, "fin.purchasing",       () => ComputePurchasingAsync(ct), new PurchasingDto());
            var productProfit    = await SafeAsync.RunAsync(_logger, "fin.productProfit",    () => ComputeProductProfitabilityAsync(ct), (IReadOnlyList<ProductProfitabilityDto>)Array.Empty<ProductProfitabilityDto>());
            var laborCost        = await SafeAsync.RunAsync(_logger, "fin.laborCost",        () => ComputeLaborCostAsync(revenue, ct), new LaborCostDto());
            var discountCost     = await SafeAsync.RunAsync(_logger, "fin.discountCost",     () => ComputeDiscountCostAsync(revenue, ct), new DiscountCostDto());
            var salesMix         = await SafeAsync.RunAsync(_logger, "fin.salesMix",         () => ComputeSalesMixAsync(revenue, ct), new SalesMixDto());
            var revenueByWaiter  = await SafeAsync.RunAsync(_logger, "fin.revenueByWaiter",  () => ComputeRevenueByWaiterAsync(ct), (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var revenueByTable   = await SafeAsync.RunAsync(_logger, "fin.revenueByTable",   () => ComputeRevenueByTableAsync(ct), (IReadOnlyList<RankedRowDto>)Array.Empty<RankedRowDto>());
            var revenueByHour    = await SafeAsync.RunAsync(_logger, "fin.revenueByHour",    () => ComputeRevenueByHourAsync(ct), (IReadOnlyList<TimeSeriesPointDto>)Array.Empty<TimeSeriesPointDto>());
            var purchaseVariance = await SafeAsync.RunAsync(_logger, "fin.purchaseVariance", () => ComputePurchasePriceVarianceAsync(ct), (IReadOnlyList<PurchasePriceVarianceDto>)Array.Empty<PurchasePriceVarianceDto>());
            var revenueByWeekday = await SafeAsync.RunAsync(_logger, "fin.revenueByWeekday", () => ComputeRevenueByWeekdayAsync(ct), (IReadOnlyList<CategorySliceDto>)Array.Empty<CategorySliceDto>());
            var refundReasons    = await SafeAsync.RunAsync(_logger, "fin.refundReasons",    () => ComputeRefundReasonsAsync(ct), (IReadOnlyList<RefundReasonDto>)Array.Empty<RefundReasonDto>());
            var taxBreakdown     = await SafeAsync.RunAsync(_logger, "fin.taxBreakdown",     () => ComputeTaxBreakdownAsync(ct), (IReadOnlyList<TaxBreakdownDto>)Array.Empty<TaxBreakdownDto>());

            PeriodComparisonDto? comparison = null;
            IReadOnlyList<GrowthRowDto> productGrowth  = Array.Empty<GrowthRowDto>();
            IReadOnlyList<GrowthRowDto> categoryGrowth = Array.Empty<GrowthRowDto>();
            if (_ctx.Filter.Compare != ComparisonMode.None)
            {
                comparison = await SafeAsync.RunAsync<PeriodComparisonDto?>(
                    _logger, "fin.comparison",
                    async () => await ComputeComparisonAsync(ct),
                    null);

                var growth = await SafeAsync.RunAsync(_logger, "fin.growth",
                    async () => await ComputeGrowthAsync(ct),
                    (Products: (IReadOnlyList<GrowthRowDto>)Array.Empty<GrowthRowDto>(),
                     Categories: (IReadOnlyList<GrowthRowDto>)Array.Empty<GrowthRowDto>()));
                productGrowth  = growth.Products;
                categoryGrowth = growth.Categories;
            }

            // Composed from the metrics above — no extra DB access, fully
            // deterministic, single source of truth for the executive band.
            var executiveSummary = BuildExecutiveSummary(revenue, profitability, foodCost, customerRevenue, comparison);
            var alerts           = BuildBusinessAlerts(revenue, profitability, foodCost, cancellations, comparison);

            return new FinancialSnapshotDto
            {
                Scope            = _ctx.Scope,
                WindowStartUtc   = _ctx.Window.StartUtc,
                WindowEndUtc     = _ctx.Window.EndUtc,
                AppliedFilter    = _ctx.Filter,
                Revenue          = revenue,
                Profitability    = profitability,
                Delivery         = delivery,
                CostSharing      = costSharing,
                RevenueByDay     = dailySeries,
                ChannelSplit     = channels,
                BestSellers      = bestSellers,
                WorstSellers     = worstSellers,
                RevenueByCashier = byCashier,
                DiscountsByUser  = discountsByUser,
                PaymentMethodAnalytics = advanced.PaymentMethods,
                OrderSourceAnalytics = advanced.OrderSources,
                PartnerAnalytics = advanced.Partners,
                CardBreakdown = advanced.CardBreakdown,
                PayMobBreakdown = advanced.PayMobBreakdown,
                Cancellations = cancellations,
                Comparison       = comparison,
                CategoryRevenue  = categoryRevenue,
                CustomerRevenue  = customerRevenue,
                FoodCost         = foodCost,
                Purchasing       = purchasing,
                ProductProfitability = productProfit,
                LaborCost        = laborCost,
                DiscountCost     = discountCost,
                SalesMix         = salesMix,
                RevenueByWaiter  = revenueByWaiter,
                RevenueByTable   = revenueByTable,
                RevenueByHour    = revenueByHour,
                ProductGrowth    = productGrowth,
                CategoryGrowth   = categoryGrowth,
                PurchasePriceVariance = purchaseVariance,
                RevenueByWeekday = revenueByWeekday,
                RefundReasons    = refundReasons,
                TaxBreakdown     = taxBreakdown,
                ExecutiveSummary = executiveSummary,
                Alerts           = alerts
            };
        }

        public async Task<CommissionAnalysisDto> BuildCommissionAnalysisAsync(CancellationToken ct)
        {
            // Tier 1 — frozen snapshots (authoritative; never recomputed).
            var snapshotPartner = await SafeAsync.RunAsync(_logger, "fin.commission.partners",
                () => QueryPartnerCommissionRowsAsync(ct),
                (IReadOnlyList<PartnerCommissionOrderRow>)Array.Empty<PartnerCommissionOrderRow>());
            var snapshotPayment = await SafeAsync.RunAsync(_logger, "fin.commission.payments",
                () => QueryPaymentCommissionRowsAsync(ct),
                (IReadOnlyList<PaymentCommissionOrderRow>)Array.Empty<PaymentCommissionOrderRow>());

            // Tier 2 — read-only reconstruction for legacy orders the cost-sharing
            // engine never stamped, using historical config (audit timeline first,
            // current provider config when the rate never changed). Snapshot rows
            // always win; reconstruction only covers orders with CostSharingCalculatedAt == null.
            var reconstruction = await SafeAsync.RunAsync(_logger, "fin.commission.reconstruct",
                () => ReconstructHistoricalCommissionAsync(ct),
                HistoricalCommissionResult.Empty);

            var partnerRows = snapshotPartner.Concat(reconstruction.PartnerRows).ToList();
            var paymentRows = snapshotPayment.Concat(reconstruction.PaymentRows).ToList();

            return new CommissionAnalysisDto
            {
                Scope          = _ctx.Scope,
                WindowStartUtc = _ctx.Window.StartUtc,
                WindowEndUtc   = _ctx.Window.EndUtc,
                AppliedFilter  = _ctx.Filter,
                Summary        = BuildCommissionSummary(partnerRows, paymentRows),
                Partners       = BuildPartnerCommissionGroups(partnerRows),
                PaymentProviders = BuildPaymentProviderCommissionGroups(paymentRows),
                Coverage       = new CommissionCoverageDto
                {
                    FromSnapshot  = snapshotPartner.Count + snapshotPayment.Count,
                    Reconstructed = reconstruction.PartnerRows.Count + reconstruction.PaymentRows.Count,
                    Excluded      = reconstruction.ExcludedCount,
                    Analyzed      = snapshotPartner.Count + snapshotPayment.Count
                                    + reconstruction.PartnerRows.Count + reconstruction.PaymentRows.Count
                                    + reconstruction.ExcludedCount
                }
            };
        }

        private async Task<IReadOnlyList<PaymentCommissionOrderRow>> QueryPaymentCommissionRowsAsync(
            CancellationToken ct)
        {
            var rows = await _q.Payments()
                .Where(OrderLifecyclePredicates.RecordedSalePayment)
                .Where(p => p.CostSharingCommissionAmount > 0m)
                .Select(p => new
                {
                    p.OrderId,
                    OrderNumber = p.Order.DisplayOrderNumber ?? p.Order.PublicOrderNumber ?? p.Order.OrderNumber,
                    DateUtc = p.Order.PaidAt ?? p.Order.CreatedAt,
                    Customer = p.Order.Customer != null
                        ? p.Order.Customer.Name
                        : p.Order.PartnerCustomerName ?? p.Order.TalabatCustomerName ?? p.Order.CustomerPhone,
                    OrderTotal = p.Order.TotalAmount,
                    GrossAmount = p.Amount,
                    p.PaymentMethodId,
                    p.PaymentMethodCode,
                    p.PaymentMethodName,
                    p.PaymentMethodNameAr,
                    p.Method,
                    CommissionAmount = p.CostSharingCommissionAmount,
                    RestaurantShare = p.CostSharingRestaurantShareAmount,
                    ProviderShare = p.CostSharingCounterpartyShareAmount
                })
                .ToListAsync(ct);

            return rows.Select(r => new PaymentCommissionOrderRow(
                r.OrderId, r.OrderNumber, r.DateUtc, FirstNotBlank(r.Customer, UnknownCustomerName)!,
                r.OrderTotal, r.GrossAmount, r.PaymentMethodId, r.PaymentMethodCode,
                r.PaymentMethodName, r.PaymentMethodNameAr, r.Method,
                r.CommissionAmount, r.RestaurantShare, r.ProviderShare)).ToList();
        }

        private async Task<IReadOnlyList<PartnerCommissionOrderRow>> QueryPartnerCommissionRowsAsync(
            CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.CostSharingTotalCommission > 0m)
                .Select(o => new
                {
                    o.Id,
                    OrderNumber = o.DisplayOrderNumber ?? o.PublicOrderNumber ?? o.OrderNumber,
                    DateUtc = o.PaidAt ?? o.CreatedAt,
                    Customer = o.Customer != null
                        ? o.Customer.Name
                        : o.PartnerCustomerName ?? o.TalabatCustomerName ?? o.CustomerPhone,
                    o.TotalAmount,
                    o.OrderSource,
                    o.DeliveryPartnerId,
                    o.DeliveryPartnerName,
                    o.DeliveryPartnerNameAr,
                    o.DeliveryPartnerCode,
                    o.CostSharingTotalCommission,
                    o.CostSharingRestaurantShare,
                    o.CostSharingCounterpartyShare,
                    PaymentCommission = o.Payments.Sum(p => p.CostSharingCommissionAmount),
                    PaymentRestaurantShare = o.Payments.Sum(p => p.CostSharingRestaurantShareAmount),
                    PaymentProviderShare = o.Payments.Sum(p => p.CostSharingCounterpartyShareAmount)
                })
                .ToListAsync(ct);

            return rows
                .Select(r => new PartnerCommissionOrderRow(
                    r.Id, r.OrderNumber, r.DateUtc, FirstNotBlank(r.Customer, UnknownCustomerName)!,
                    r.TotalAmount, r.OrderSource, r.DeliveryPartnerId, r.DeliveryPartnerName,
                    r.DeliveryPartnerNameAr, r.DeliveryPartnerCode,
                    Math.Max(0m, r.CostSharingTotalCommission - r.PaymentCommission),
                    Math.Max(0m, r.CostSharingRestaurantShare - r.PaymentRestaurantShare),
                    Math.Max(0m, r.CostSharingCounterpartyShare - r.PaymentProviderShare)))
                .Where(r => r.CommissionAmount > 0m)
                .ToList();
        }

        private static IReadOnlyList<PartnerCommissionDto> BuildPartnerCommissionGroups(
            IReadOnlyList<PartnerCommissionOrderRow> rows)
            => rows
                .GroupBy(ResolvePartnerDescriptor)
                .Select(group => BuildPartnerCommissionGroup(group.Key, group))
                .OrderByDescending(row => row.CommissionAmount)
                .ToList();

        private static PartnerCommissionDto BuildPartnerCommissionGroup(
            PartnerDescriptor descriptor,
            IEnumerable<PartnerCommissionOrderRow> rows)
        {
            var list = rows.ToList();
            var gross = list.Sum(row => row.OrderTotal);
            var commission = list.Sum(row => row.CommissionAmount);
            var restaurantShare = list.Sum(row => row.RestaurantShare);
            var orderCount = list.Select(row => row.OrderId).Distinct().Count();

            return new PartnerCommissionDto
            {
                Key = descriptor.Key,
                Partner = descriptor.Name,
                PartnerAr = descriptor.NameAr,
                NumberOfOrders = orderCount,
                GrossSales = RoundCurrency(gross),
                CommissionPercentage = Percent(commission, gross),
                CommissionAmount = RoundCurrency(commission),
                RestaurantShare = RoundCurrency(restaurantShare),
                PartnerShare = RoundCurrency(list.Sum(row => row.ProviderShare)),
                RestaurantFinalRevenue = RoundCurrency(gross - restaurantShare),
                AverageCommissionPerOrder = orderCount == 0 ? 0 : RoundCurrency(commission / orderCount),
                Basis = ResolveGroupBasis(list.Select(row => row.Basis)),
                Orders = list.Select(BuildPartnerCommissionOrder).OrderByDescending(row => row.DateUtc).ToList()
            };
        }

        private static IReadOnlyList<PaymentProviderCommissionDto> BuildPaymentProviderCommissionGroups(
            IReadOnlyList<PaymentCommissionOrderRow> rows)
            => rows
                .GroupBy(ResolvePaymentDescriptor)
                .Select(group => BuildPaymentProviderCommissionGroup(group.Key, group))
                .OrderByDescending(row => row.FeeAmount)
                .ToList();

        private static PaymentProviderCommissionDto BuildPaymentProviderCommissionGroup(
            PaymentDescriptor descriptor,
            IEnumerable<PaymentCommissionOrderRow> rows)
        {
            var list = rows.ToList();
            var gross = list.Sum(row => row.GrossAmount);
            var fee = list.Sum(row => row.CommissionAmount);
            var restaurantShare = list.Sum(row => row.RestaurantShare);

            return new PaymentProviderCommissionDto
            {
                Key = descriptor.Key,
                PaymentMethod = descriptor.Name,
                PaymentMethodAr = descriptor.NameAr,
                Transactions = list.Count,
                GrossAmount = RoundCurrency(gross),
                FeePercentage = Percent(fee, gross),
                FeeAmount = RoundCurrency(fee),
                RestaurantShare = RoundCurrency(restaurantShare),
                ProviderShare = RoundCurrency(list.Sum(row => row.ProviderShare)),
                NetAmountReceived = RoundCurrency(gross - restaurantShare),
                Basis = ResolveGroupBasis(list.Select(row => row.Basis)),
                Orders = BuildPaymentCommissionOrders(list)
            };
        }

        private static CommissionSummaryDto BuildCommissionSummary(
            IReadOnlyList<PartnerCommissionOrderRow> partnerRows,
            IReadOnlyList<PaymentCommissionOrderRow> paymentRows)
        {
            var partnerCommission = partnerRows.Sum(row => row.CommissionAmount);
            var paymentFees = paymentRows.Sum(row => row.CommissionAmount);
            var transactionCount = partnerRows.Select(row => row.OrderId)
                .Concat(paymentRows.Select(row => row.OrderId))
                .Distinct()
                .Count();
            var grossBase = partnerRows.Sum(row => row.OrderTotal)
                + paymentRows.Sum(row => row.GrossAmount);
            var totalCommission = partnerCommission + paymentFees;

            return new CommissionSummaryDto
            {
                TotalPartnerCommission = RoundCurrency(partnerCommission),
                TotalPaymentFees = RoundCurrency(paymentFees),
                TotalRestaurantCostSharing = RoundCurrency(
                    partnerRows.Sum(row => row.RestaurantShare) + paymentRows.Sum(row => row.RestaurantShare)),
                TotalProviderCostSharing = RoundCurrency(
                    partnerRows.Sum(row => row.ProviderShare) + paymentRows.Sum(row => row.ProviderShare)),
                TotalCommissionTransactions = transactionCount,
                AverageCommissionPercentage = Percent(totalCommission, grossBase),
                AverageFeePerOrder = transactionCount == 0 ? 0 : RoundCurrency(totalCommission / transactionCount)
            };
        }

        private static IReadOnlyList<CommissionOrderDto> BuildPaymentCommissionOrders(
            IEnumerable<PaymentCommissionOrderRow> rows)
            => rows
                .GroupBy(row => row.OrderId)
                .Select(group =>
                {
                    var first = group.First();
                    var gross = group.Sum(row => row.GrossAmount);
                    var fee = group.Sum(row => row.CommissionAmount);
                    var restaurantShare = group.Sum(row => row.RestaurantShare);
                    return new CommissionOrderDto
                    {
                        OrderId = first.OrderId,
                        OrderNumber = first.OrderNumber,
                        DateUtc = first.DateUtc,
                        Customer = first.Customer,
                        OrderTotal = RoundCurrency(first.OrderTotal),
                        CommissionPercentage = Percent(fee, gross),
                        CommissionAmount = RoundCurrency(fee),
                        RestaurantShare = RoundCurrency(restaurantShare),
                        ProviderShare = RoundCurrency(group.Sum(row => row.ProviderShare)),
                        NetAmount = RoundCurrency(gross - restaurantShare),
                        Basis = first.Basis
                    };
                })
                .OrderByDescending(row => row.DateUtc)
                .ToList();

        private static CommissionOrderDto BuildPartnerCommissionOrder(PartnerCommissionOrderRow row)
            => new()
            {
                OrderId = row.OrderId,
                OrderNumber = row.OrderNumber,
                DateUtc = row.DateUtc,
                Customer = row.Customer,
                OrderTotal = RoundCurrency(row.OrderTotal),
                CommissionPercentage = Percent(row.CommissionAmount, row.OrderTotal),
                CommissionAmount = RoundCurrency(row.CommissionAmount),
                RestaurantShare = RoundCurrency(row.RestaurantShare),
                ProviderShare = RoundCurrency(row.ProviderShare),
                NetAmount = RoundCurrency(row.OrderTotal - row.RestaurantShare),
                Basis = row.Basis
            };

        private static PaymentDescriptor ResolvePaymentDescriptor(PaymentCommissionOrderRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.PaymentMethodCode)
                || !string.IsNullOrWhiteSpace(row.PaymentMethodName))
            {
                var key = FirstNotBlank(row.PaymentMethodCode, row.PaymentMethodName)!;
                return new PaymentDescriptor(
                    key,
                    FirstNotBlank(row.PaymentMethodName, row.PaymentMethodCode)!,
                    row.PaymentMethodNameAr,
                    false);
            }

            if (row.PaymentMethodId.HasValue)
                return new PaymentDescriptor(
                    $"PMID:{row.PaymentMethodId.Value:N}",
                    FirstNotBlank(row.PaymentMethodName, row.Method, UnknownPaymentMethodName)!,
                    row.PaymentMethodNameAr,
                    false);

            var legacy = OrderPaymentHelper.ResolveLegacyBucket(row.Method);
            return new PaymentDescriptor(legacy.Key, legacy.Label, legacy.LabelAr, false);
        }

        private static PartnerDescriptor ResolvePartnerDescriptor(PartnerCommissionOrderRow row)
        {
            if (row.OrderSource == OrderSource.Talabat)
                return new PartnerDescriptor("TALABAT", "Talabat", "طلبات", true);

            if (row.DeliveryPartnerId.HasValue)
                return new PartnerDescriptor(
                    $"ID:{row.DeliveryPartnerId.Value:N}",
                    FirstNotBlank(row.DeliveryPartnerName, row.DeliveryPartnerCode, UnknownPartnerName)!,
                    row.DeliveryPartnerNameAr,
                    false);

            if (!string.IsNullOrWhiteSpace(row.DeliveryPartnerCode))
                return new PartnerDescriptor(
                    $"CODE:{row.DeliveryPartnerCode.Trim()}",
                    FirstNotBlank(row.DeliveryPartnerName, row.DeliveryPartnerCode)!,
                    row.DeliveryPartnerNameAr,
                    false);

            return new PartnerDescriptor(
                $"NAME:{FirstNotBlank(row.DeliveryPartnerName, UnknownPartnerName)}",
                FirstNotBlank(row.DeliveryPartnerName, UnknownPartnerName)!,
                row.DeliveryPartnerNameAr,
                false);
        }

        private static string ResolveGroupBasis(IEnumerable<string> bases)
        {
            var distinct = bases.Distinct().ToList();
            if (distinct.Count == 0) return CommissionBasis.Snapshot;
            return distinct.Count == 1 ? distinct[0] : CommissionBasis.Mixed;
        }

        // ─── Historical commission reconstruction (read-only) ──────────────────
        // For legacy orders the cost-sharing engine never stamped
        // (CostSharingCalculatedAt == null), rebuild commission from the most
        // accurate historical source available: the audited rate effective at the
        // order's completion date, falling back to current provider config when the
        // rate never changed. Snapshots always take priority and are never touched.
        // Nothing here writes to the database.
        private async Task<HistoricalCommissionResult> ReconstructHistoricalCommissionAsync(CancellationToken ct)
        {
            var directory = await BuildCommissionConfigDirectoryAsync(ct);
            if (directory.PartnersById.Count == 0 && directory.MethodsById.Count == 0)
                return HistoricalCommissionResult.Empty;

            var partner = await ReconstructPartnerRowsAsync(directory, ct);
            var payment = await ReconstructPaymentRowsAsync(directory, ct);

            return new HistoricalCommissionResult(
                partner.Rows, payment.Rows, partner.Excluded + payment.Excluded);
        }

        private async Task<(IReadOnlyList<PartnerCommissionOrderRow> Rows, int Excluded)>
            ReconstructPartnerRowsAsync(CommissionConfigDirectory directory, CancellationToken ct)
        {
            // Candidates = delivery/partner orders whose PARTNER commission leg has no
            // recorded amount. Tier-1 already emits orders with a stored partner leg
            // (CostSharingTotalCommission − payment commission > 0), so we skip those
            // to avoid double-counting. A recorded leg of 0 means the engine never
            // computed partner commission for this order (legacy, or the native
            // Talabat source which the live engine never stamps) → reconstruct it.
            var candidates = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.DeliveryPartnerId != null
                            || o.OrderSource == OrderSource.Talabat
                            || o.OrderSource == OrderSource.DeliveryPartner)
                .Select(o => new
                {
                    o.Id,
                    OrderNumber = o.DisplayOrderNumber ?? o.PublicOrderNumber ?? o.OrderNumber,
                    DateUtc = o.PaidAt ?? o.CreatedAt,
                    Customer = o.Customer != null
                        ? o.Customer.Name
                        : o.PartnerCustomerName ?? o.TalabatCustomerName ?? o.CustomerPhone,
                    o.TotalAmount,
                    o.OrderSource,
                    o.DeliveryPartnerId,
                    o.DeliveryPartnerName,
                    o.DeliveryPartnerNameAr,
                    o.DeliveryPartnerCode,
                    RecordedPartnerCommission =
                        o.CostSharingTotalCommission - o.Payments.Sum(p => p.CostSharingCommissionAmount)
                })
                .ToListAsync(ct);

            var rows = new List<PartnerCommissionOrderRow>();
            var excluded = 0;
            foreach (var c in candidates)
            {
                if (c.RecordedPartnerCommission > 0m) continue; // Tier-1 already has this partner leg

                var cfg = ResolvePartnerConfig(directory, c.DeliveryPartnerId,
                    c.DeliveryPartnerCode, c.DeliveryPartnerName, c.OrderSource);
                if (cfg == null) { excluded++; continue; }

                var rule = ResolveEffectiveRule(directory, cfg, c.DateUtc);
                if (rule.CommissionPct <= 0m) continue; // resolvable but genuinely no commission

                var calc = ComputeReconstructed(c.TotalAmount, rule, CostSharingTargetType.DeliveryPartner, cfg);
                if (calc.TotalCommission <= 0m) continue;

                rows.Add(new PartnerCommissionOrderRow(
                    c.Id, c.OrderNumber, c.DateUtc, FirstNotBlank(c.Customer, UnknownCustomerName)!,
                    c.TotalAmount, c.OrderSource, c.DeliveryPartnerId,
                    c.DeliveryPartnerName, c.DeliveryPartnerNameAr, c.DeliveryPartnerCode,
                    calc.TotalCommission, calc.RestaurantShare, calc.CounterpartyShare,
                    CommissionBasis.Reconstructed));
            }

            return (rows, excluded);
        }

        private async Task<(IReadOnlyList<PaymentCommissionOrderRow> Rows, int Excluded)>
            ReconstructPaymentRowsAsync(CommissionConfigDirectory directory, CancellationToken ct)
        {
            // Tier-1 emits payments with a recorded fee (CostSharingCommissionAmount > 0),
            // so the complement (== 0) is exactly the set with no recorded fee: legacy
            // card payments, or payments stamped before the method's fee was configured.
            var candidates = await _q.Payments()
                .Where(OrderLifecyclePredicates.RecordedSalePayment)
                .Where(p => p.CostSharingCommissionAmount == 0m)
                // Exclude cash at the source (no processing fee) but keep any payment
                // carrying a method link; the in-memory IsCashMethod guard is the backstop.
                .Where(p => p.PaymentMethodId != null
                            || (p.Method != null
                                && !p.Method.ToLower().Contains("cash")
                                && !p.Method.ToLower().Contains("nakit")))
                .Select(p => new
                {
                    p.OrderId,
                    OrderNumber = p.Order.DisplayOrderNumber ?? p.Order.PublicOrderNumber ?? p.Order.OrderNumber,
                    DateUtc = p.Order.PaidAt ?? p.Order.CreatedAt,
                    Customer = p.Order.Customer != null
                        ? p.Order.Customer.Name
                        : p.Order.PartnerCustomerName ?? p.Order.TalabatCustomerName ?? p.Order.CustomerPhone,
                    OrderTotal = p.Order.TotalAmount,
                    GrossAmount = p.Amount,
                    p.PaymentMethodId,
                    p.PaymentMethodCode,
                    p.PaymentMethodName,
                    p.PaymentMethodNameAr,
                    p.Method
                })
                .ToListAsync(ct);

            var rows = new List<PaymentCommissionOrderRow>();
            var excluded = 0;
            foreach (var c in candidates)
            {
                if (OrderPaymentHelper.IsCashMethod(c.Method)) continue; // cash carries no processing fee

                var cfg = ResolvePaymentConfig(directory, c.PaymentMethodId,
                    c.PaymentMethodCode, c.PaymentMethodName, c.Method);
                if (cfg == null) { excluded++; continue; }

                var rule = ResolveEffectiveRule(directory, cfg, c.DateUtc);
                if (rule.CommissionPct <= 0m) continue;

                var calc = ComputeReconstructed(c.GrossAmount, rule, CostSharingTargetType.PaymentMethod, cfg);
                if (calc.TotalCommission <= 0m) continue;

                rows.Add(new PaymentCommissionOrderRow(
                    c.OrderId, c.OrderNumber, c.DateUtc, FirstNotBlank(c.Customer, UnknownCustomerName)!,
                    c.OrderTotal, c.GrossAmount,
                    c.PaymentMethodId, c.PaymentMethodCode, c.PaymentMethodName, c.PaymentMethodNameAr, c.Method,
                    calc.TotalCommission, calc.RestaurantShare, calc.CounterpartyShare,
                    CommissionBasis.Reconstructed));
            }

            return (rows, excluded);
        }

        private async Task<CommissionConfigDirectory> BuildCommissionConfigDirectoryAsync(CancellationToken ct)
        {
            var partners = await _db.DeliveryPartners.AsNoTracking()
                .Select(p => new ProviderConfig(p.Id, p.Code, p.Name, p.NameAr,
                    p.CostSharingMode, p.CostSharingCommissionPercentage,
                    p.CostSharingRestaurantPercentage, p.CostSharingCounterpartyPercentage))
                .ToListAsync(ct);

            var methods = await _db.PaymentMethods.AsNoTracking()
                .Select(m => new ProviderConfig(m.Id, m.Code, m.NameEn, m.NameAr,
                    m.CostSharingMode, m.CostSharingCommissionPercentage,
                    m.CostSharingRestaurantPercentage, m.CostSharingCounterpartyPercentage))
                .ToListAsync(ct);

            var audits = await _db.ConfigAuditLogs.AsNoTracking()
                .Where(a => a.EventType == ConfigAuditEventType.CostSharingConfigChanged && a.TargetId != null)
                .OrderBy(a => a.CreatedAt)
                .Select(a => new { a.TargetId, a.CreatedAt, a.PreviousValue, a.NewValue })
                .ToListAsync(ct);

            var timelines = audits
                .GroupBy(a => a.TargetId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(a => a.CreatedAt)
                          .Select(a => new AuditPoint(a.CreatedAt, a.PreviousValue, a.NewValue))
                          .ToList());

            return new CommissionConfigDirectory
            {
                PartnersById   = partners.ToDictionary(p => p.Id),
                PartnersByCode = IndexProvidersByKey(partners, p => p.Code),
                PartnersByName = IndexProvidersByKey(partners, p => p.Name),
                MethodsById    = methods.ToDictionary(m => m.Id),
                MethodsByCode  = IndexProvidersByKey(methods, m => m.Code),
                MethodsByName  = IndexProvidersByKey(methods, m => m.Name),
                Timelines      = timelines
            };
        }

        private static IReadOnlyDictionary<string, ProviderConfig> IndexProvidersByKey(
            IEnumerable<ProviderConfig> items, Func<ProviderConfig, string?> keySelector)
        {
            var map = new Dictionary<string, ProviderConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                var key = keySelector(item)?.Trim();
                if (string.IsNullOrEmpty(key)) continue;
                // First write wins: on a duplicate key keep the earlier provider and
                // skip the ambiguous one, so resolution stays deterministic.
                map.TryAdd(key, item);
            }
            return map;
        }

        private static ProviderConfig? ResolvePartnerConfig(
            CommissionConfigDirectory directory, Guid? id, string? code, string? name, OrderSource source)
        {
            if (id.HasValue && directory.PartnersById.TryGetValue(id.Value, out var byId))
                return byId;
            return ResolveByKeys(directory.PartnersByCode, directory.PartnersByName, code, name, source.ToString());
        }

        private static ProviderConfig? ResolvePaymentConfig(
            CommissionConfigDirectory directory, Guid? id, string? code, string? name, string? method)
        {
            if (id.HasValue && directory.MethodsById.TryGetValue(id.Value, out var byId))
                return byId;
            return ResolveByKeys(directory.MethodsByCode, directory.MethodsByName, code, name, method);
        }

        private static ProviderConfig? ResolveByKeys(
            IReadOnlyDictionary<string, ProviderConfig> byCode,
            IReadOnlyDictionary<string, ProviderConfig> byName,
            params string?[] candidates)
        {
            foreach (var raw in candidates)
            {
                var key = raw?.Trim();
                if (string.IsNullOrEmpty(key)) continue;
                if (byCode.TryGetValue(key, out var byCodeMatch)) return byCodeMatch;
                if (byName.TryGetValue(key, out var byNameMatch)) return byNameMatch;
            }
            return null;
        }

        private static EffectiveRule ResolveEffectiveRule(
            CommissionConfigDirectory directory, ProviderConfig cfg, DateTime atUtc)
        {
            if (directory.Timelines.TryGetValue(cfg.Id, out var timeline) && timeline.Count > 0)
            {
                var prior = timeline.LastOrDefault(p => p.AtUtc <= atUtc);
                // Order predates the first recorded change → use the state before that
                // first change (its PreviousValue) when available, else its NewValue.
                var json = prior?.NewJson ?? timeline[0].PreviousJson ?? timeline[0].NewJson;
                var parsed = ParseAuditedRule(json);
                // Only honor a *real* historical rate. A recorded 0% is the pre-configuration
                // default (the provider existed before commission was ever set up), not a
                // genuine "0% deal" — falling through to current config means historical
                // orders reflect the rate the owner actually configured, instead of vanishing.
                if (parsed != null && parsed.CommissionPct > 0m) return parsed;
            }

            // No audit history, or only a pre-configuration 0%: the current configuration
            // is the best available representation of this provider's commission.
            return new EffectiveRule(cfg.Mode, cfg.CommissionPct, cfg.RestaurantPct, cfg.CounterpartyPct);
        }

        private static EffectiveRule? ParseAuditedRule(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var dto = JsonSerializer.Deserialize<AuditedRuleJson>(json, AuditJsonOptions);
                if (dto == null) return null;
                return new EffectiveRule(
                    (CostSharingMode)dto.Mode,
                    dto.CostSharingCommissionPercentage,
                    dto.RestaurantPercentage,
                    dto.CounterpartyPercentage);
            }
            catch
            {
                return null;
            }
        }

        private static CostSharingCalculation ComputeReconstructed(
            decimal basis, EffectiveRule rule, CostSharingTargetType targetType, ProviderConfig cfg)
        {
            var snapshot = new CostSharingRuleSnapshot(
                targetType, cfg.Id, cfg.Name, cfg.Code,
                rule.Mode, CostSharingScope.PerPartnerOrCard,
                rule.CommissionPct, rule.RestaurantPct, rule.CounterpartyPct);
            return CostSharingHelper.Calculate(basis, snapshot);
        }

        private static readonly JsonSerializerOptions AuditJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static decimal Percent(decimal numerator, decimal denominator)
            => denominator == 0m ? 0m : Math.Round(numerator / denominator * 100m, 2);

        private static decimal RoundCurrency(decimal amount)
            => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        private static ExecutiveSummaryDto BuildExecutiveSummary(
            RevenueBreakdownDto revenue,
            ProfitabilityDto profitability,
            FoodCostDto foodCost,
            CustomerRevenueDto customerRevenue,
            PeriodComparisonDto? comparison)
        {
            var refundPct = revenue.Gross == 0
                ? 0m
                : Math.Round(revenue.Refunds / revenue.Gross * 100m, 2);

            var prevRev    = comparison?.PreviousRevenue;
            var prevProfit = comparison?.PreviousProfitability;
            decimal? prevGrossMargin = prevProfit is null || prevRev is null
                ? null
                : prevRev.Net == 0 ? 0m : Math.Round(prevProfit.GrossProfit / prevRev.Net * 100m, 2);

            return new ExecutiveSummaryDto
            {
                Revenue            = Kpi(revenue.Net, prevRev?.Net),
                NetProfit          = Kpi(profitability.NetProfit, prevProfit?.NetProfit),
                GrossMarginPercent = Kpi(profitability.GrossMarginPercent, prevGrossMargin),
                FoodCostPercent    = Kpi(foodCost.FoodCostPercentage, null),
                DiscountPercent    = Kpi(revenue.DiscountPercentage, null),
                RefundPercent      = Kpi(refundPct, null),
                AverageOrderValue  = Kpi(revenue.AverageOrderValue, prevRev?.AverageOrderValue),
                Orders             = Kpi(revenue.OrderCount, (decimal?)prevRev?.OrderCount),
                Customers          = Kpi(customerRevenue.UniqueCustomers, null),
                RevenueGrowthPercent = comparison?.RevenueChangePercent ?? 0m,
                LastUpdatedUtc     = DateTime.UtcNow
            };

            static KpiValueDto<decimal> Kpi(decimal current, decimal? previous)
            {
                if (previous is null) return new KpiValueDto<decimal>(current);
                var change = previous.Value == 0
                    ? (current > 0 ? 100m : 0m)
                    : Math.Round((current - previous.Value) / previous.Value * 100m, 2);
                return new KpiValueDto<decimal>(current, previous.Value, change);
            }
        }

        private static IReadOnlyList<DashboardAlertDto> BuildBusinessAlerts(
            RevenueBreakdownDto revenue,
            ProfitabilityDto profitability,
            FoodCostDto foodCost,
            CancellationSummaryDto cancellations,
            PeriodComparisonDto? comparison)
        {
            var now = DateTime.UtcNow;
            var alerts = new List<DashboardAlertDto>();

            void Add(string code, string severity, string en, string ar,
                string? metaEn = null, string? metaAr = null)
                => alerts.Add(new DashboardAlertDto(code, severity, en, metaEn, now, ar, metaAr));

            // Food cost above threshold (only meaningful once a cost is recorded).
            if (foodCost.ActualFoodCost > 0
                && foodCost.FoodCostPercentage > FinancialAlertThresholds.FoodCostPercentWarning)
                Add("FOOD_COST_HIGH",
                    foodCost.FoodCostPercentage > FinancialAlertThresholds.FoodCostPercentDanger ? "danger" : "warning",
                    $"Food cost is {foodCost.FoodCostPercentage:0.#}% of net revenue",
                    $"تكلفة الطعام {foodCost.FoodCostPercentage:0.#}٪ من صافي الإيراد",
                    $"Target ≤ {FinancialAlertThresholds.FoodCostPercentWarning:0}%",
                    $"المستهدف ≤ {FinancialAlertThresholds.FoodCostPercentWarning:0}٪");

            // Actual cost running well over the theoretical recipe cost.
            if (foodCost.HasTheoreticalData
                && foodCost.FoodCostVariancePercentage > FinancialAlertThresholds.FoodCostVariancePercentWarning)
                Add("FOOD_COST_VARIANCE", "warning",
                    $"Actual food cost is {foodCost.FoodCostVariancePercentage:0.#}% over theoretical",
                    $"التكلفة الفعلية أعلى من النظرية بنسبة {foodCost.FoodCostVariancePercentage:0.#}٪",
                    "Check portioning, waste and purchase prices",
                    "راجع التحصيص والهدر وأسعار الشراء");

            // High refund rate.
            var refundPct = revenue.Gross == 0 ? 0m : revenue.Refunds / revenue.Gross * 100m;
            if (refundPct > FinancialAlertThresholds.RefundPercentWarning)
                Add("HIGH_REFUND_RATE",
                    refundPct > FinancialAlertThresholds.RefundPercentDanger ? "danger" : "warning",
                    $"Refunds are {refundPct:0.#}% of gross revenue",
                    $"المرتجعات {refundPct:0.#}٪ من الإيراد الإجمالي",
                    $"Target ≤ {FinancialAlertThresholds.RefundPercentWarning:0}%",
                    $"المستهدف ≤ {FinancialAlertThresholds.RefundPercentWarning:0}٪");

            // High pre-payment cancellation rate.
            var salesBase = revenue.OrderCount + cancellations.CancelledBeforePaymentCount;
            var cancelPct = salesBase == 0
                ? 0m
                : (decimal)cancellations.CancelledBeforePaymentCount / salesBase * 100m;
            if (cancelPct > FinancialAlertThresholds.CancellationPercentWarning)
                Add("HIGH_CANCELLATION_RATE", "warning",
                    $"{cancelPct:0.#}% of orders were cancelled before payment",
                    $"{cancelPct:0.#}٪ من الطلبات أُلغيت قبل الدفع",
                    $"{cancellations.CancelledBeforePaymentCount} orders",
                    $"{cancellations.CancelledBeforePaymentCount} طلب");

            // High discount rate.
            if (revenue.DiscountPercentage > FinancialAlertThresholds.DiscountPercentWarning)
                Add("HIGH_DISCOUNT_RATE", "warning",
                    $"Discounts are {revenue.DiscountPercentage:0.#}% of gross revenue",
                    $"الخصومات {revenue.DiscountPercentage:0.#}٪ من الإيراد الإجمالي",
                    $"Target ≤ {FinancialAlertThresholds.DiscountPercentWarning:0}%",
                    $"المستهدف ≤ {FinancialAlertThresholds.DiscountPercentWarning:0}٪");

            // Profit / margin health.
            if (profitability.NetProfit < 0)
                Add("PROFIT_NEGATIVE", "danger",
                    "Net profit is negative for this period",
                    "صافي الربح سالب لهذه الفترة",
                    "Costs exceed net revenue",
                    "التكاليف تتجاوز صافي الإيراد");
            else if (revenue.Net > 0
                && profitability.MarginPercent < FinancialAlertThresholds.NetMarginPercentWarning)
                Add("LOW_MARGIN", "warning",
                    $"Net margin is {profitability.MarginPercent:0.#}%",
                    $"هامش صافي الربح {profitability.MarginPercent:0.#}٪",
                    $"Target ≥ {FinancialAlertThresholds.NetMarginPercentWarning:0}%",
                    $"المستهدف ≥ {FinancialAlertThresholds.NetMarginPercentWarning:0}٪");

            // Revenue declining vs the comparison window.
            if (comparison is not null
                && comparison.RevenueChangePercent < FinancialAlertThresholds.RevenueDeclinePercentWarning)
                Add("REVENUE_DECLINING", "warning",
                    $"Revenue is down {Math.Abs(comparison.RevenueChangePercent):0.#}% vs the previous period",
                    $"انخفض الإيراد بنسبة {Math.Abs(comparison.RevenueChangePercent):0.#}٪ مقارنة بالفترة السابقة",
                    null, null);

            return alerts;
        }

        private static async Task<DeliveryMetricsDto> ComputeDeliveryAsync(IMetricQueryBuilder q, CancellationToken ct)
        {
            var rows = await q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.OrderType == OrderType.Delivery)
                .Select(o => new { o.DeliveryFee, o.DeliveryCost })
                .ToListAsync(ct);

            var revenue = rows.Sum(o => o.DeliveryFee ?? 0m);
            var cost = rows.Sum(o => o.DeliveryCost ?? 0m);

            return new DeliveryMetricsDto
            {
                OrderCount = rows.Count,
                Revenue = Math.Round(revenue, 2),
                Cost = Math.Round(cost, 2),
                Profit = Math.Round(revenue - cost, 2)
            };
        }

        private static async Task<CostSharingMetricsDto> ComputeCostSharingAsync(
            IMetricQueryBuilder q,
            CancellationToken ct)
        {
            var aggregate = await q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalCommission = g.Sum(o => o.CostSharingTotalCommission),
                    RestaurantShare = g.Sum(o => o.CostSharingRestaurantShare),
                    CounterpartyShare = g.Sum(o => o.CostSharingCounterpartyShare),
                    NetRevenueAfterCostSharing = g.Sum(o =>
                        (o.NetRestaurantRevenue > 0m ? o.NetRestaurantRevenue : o.TotalAmount)
                        - o.CostSharingRestaurantShare)
                })
                .FirstOrDefaultAsync(ct);

            var refunds = await q.RefundLogs()
                .SumAsync(r => (decimal?)r.RefundAmount, ct) ?? 0m;

            return new CostSharingMetricsDto
            {
                TotalCommission = Math.Round(aggregate?.TotalCommission ?? 0m, 2),
                CommissionPaidByRestaurant = Math.Round(aggregate?.RestaurantShare ?? 0m, 2),
                CommissionPaidByCounterparty = Math.Round(aggregate?.CounterpartyShare ?? 0m, 2),
                NetRevenueAfterCostSharing = Math.Round(
                    (aggregate?.NetRevenueAfterCostSharing ?? 0m) - refunds, 2)
            };
        }

        private static async Task<RevenueBreakdownDto> ComputeRevenueAsync(IMetricQueryBuilder q, MetricWindow w, CancellationToken ct)
        {
            var paidQ = q.Orders().Where(OrderLifecyclePredicates.RecordedSale);

            var agg = await paidQ.Select(o => new
            {
                o.TotalAmount,
                o.Subtotal,
                o.DiscountAmount,
                o.VoucherDiscountAmount,
                o.TaxAmount,
                o.ServiceChargeAmount
            }).ToListAsync(ct);

            var refunds = await q.RefundLogs().SumAsync(r => (decimal?)r.RefundAmount, ct) ?? 0m;

            var gross    = agg.Sum(x => x.Subtotal);
            var discount = agg.Sum(x => x.DiscountAmount);
            var voucher  = agg.Sum(x => x.VoucherDiscountAmount);
            var tax      = agg.Sum(x => x.TaxAmount);
            var service  = agg.Sum(x => x.ServiceChargeAmount);
            var total    = agg.Sum(x => x.TotalAmount);
            var net      = total - refunds;
            var count    = agg.Count;
            var aov      = count == 0 ? 0m : Math.Round(total / count, 2);

            return new RevenueBreakdownDto
            {
                Gross             = Math.Round(gross, 2),
                Discounts         = Math.Round(discount, 2),
                DiscountPercentage = gross == 0 ? 0 : Math.Round(discount / gross * 100m, 2),
                Vouchers          = Math.Round(voucher, 2),
                Refunds           = Math.Round(refunds, 2),
                Net               = Math.Round(net, 2),
                Tax               = Math.Round(tax, 2),
                ServiceCharge     = Math.Round(service, 2),
                OrderCount        = count,
                AverageOrderValue = aov
            };
        }

        private static async Task<ProfitabilityDto> ComputeProfitabilityAsync(
            IMetricQueryBuilder q, MetricWindow w, RevenueBreakdownDto revenue, CancellationToken ct)
        {
            var cogs = await SumRecordedSaleCogsAsync(q, ct);
            var labor = await q.TimeEntries().SumAsync(te => (decimal?)te.TotalCost, ct) ?? 0m;

            var gross = revenue.Net - cogs;
            var net   = gross - labor;
            var primeCost = cogs + labor;
            var margin = revenue.Net == 0 ? 0 : Math.Round(net / revenue.Net * 100m, 2);
            var grossMargin = revenue.Net == 0 ? 0 : Math.Round(gross / revenue.Net * 100m, 2);
            var primeCostPct = revenue.Net == 0 ? 0 : Math.Round(primeCost / revenue.Net * 100m, 2);
            var profitPerOrder = revenue.OrderCount == 0 ? 0 : Math.Round(net / revenue.OrderCount, 2);

            return new ProfitabilityDto
            {
                Cogs                = Math.Round(cogs, 2),
                LaborCost           = Math.Round(labor, 2),
                PrimeCost           = Math.Round(primeCost, 2),
                PrimeCostPercentage = primeCostPct,
                GrossProfit         = Math.Round(gross, 2),
                GrossMarginPercent  = grossMargin,
                NetProfit           = Math.Round(net, 2),
                MarginPercent       = margin,
                ProfitPerOrder      = profitPerOrder
            };
        }

        private static async Task<decimal> SumRecordedSaleCogsAsync(IMetricQueryBuilder q, CancellationToken ct)
            => await q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .SumAsync(oi => (decimal?)oi.StockDeductedCost, ct) ?? 0m;

        private static async Task<IReadOnlyList<TimeSeriesPointDto>> ComputeDailySeriesAsync(IMetricQueryBuilder q, CancellationToken ct)
        {
            var rows = await q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .GroupBy(o => new
                {
                    Year = (o.PaidAt ?? o.CreatedAt).Year,
                    Month = (o.PaidAt ?? o.CreatedAt).Month,
                    Day = (o.PaidAt ?? o.CreatedAt).Day
                })
                .Select(g => new
                {
                    g.Key.Year, g.Key.Month, g.Key.Day,
                    Total = g.Sum(o => o.TotalAmount),
                    Count = g.Count()
                })
                .ToListAsync(ct);

            return rows
                .Select(r => new TimeSeriesPointDto(
                    new DateTime(r.Year, r.Month, r.Day, 0, 0, 0, DateTimeKind.Utc),
                    r.Total, r.Count))
                .OrderBy(p => p.BucketStartUtc)
                .ToList();
        }

        private static IReadOnlyList<CategorySliceDto> BuildChannelSplit(
            IReadOnlyList<PaymentMethodAnalyticsDto> rows)
        {
            var total = rows.Sum(row => row.NetRevenue + row.RefundAmount);
            return rows
                .Select(row => new CategorySliceDto(
                    row.Name,
                    row.NameAr,
                    Math.Round(row.NetRevenue + row.RefundAmount, 2),
                    total == 0
                        ? 0
                        : Math.Round((row.NetRevenue + row.RefundAmount) / total * 100m, 2),
                    row.TotalOrders))
                .ToList();
        }

        private async Task<AdvancedFinancialAnalytics> ComputeAdvancedAnalyticsAsync(CancellationToken ct)
        {
            var payments = await QueryPaymentAggregatesAsync(ct);
            var legacyPayments = await QueryLegacyPaymentAggregatesAsync(ct);
            var sources = await QueryOrderSourceAggregatesAsync(ct);
            var partners = await QueryPartnerAggregatesAsync(ct);
            var settlements = await QueryPartnerSettlementAggregatesAsync(ct);
            var refunds = await QueryRefundAggregatesAsync(ct);
            var cancellations = await QueryCancellationAggregatesAsync(ct);
            var paymentMetadata = await LoadPaymentMetadataAsync(payments, ct);
            var partnerMetadata = await LoadPartnerMetadataAsync(partners, ct);

            var paymentAnalytics = BuildPaymentMethodAnalytics(
                payments, legacyPayments, refunds, paymentMetadata);
            return new AdvancedFinancialAnalytics(
                paymentAnalytics,
                BuildOrderSourceAnalytics(sources, refunds, cancellations),
                BuildPartnerAnalytics(partners, settlements, refunds, partnerMetadata),
                BuildCardBreakdown(paymentAnalytics),
                BuildPayMobBreakdown(paymentAnalytics));
        }

        private async Task<IReadOnlyList<PaymentAggregateRow>> QueryPaymentAggregatesAsync(
            CancellationToken ct)
        {
            var rows = await _q.Payments()
                .Where(OrderLifecyclePredicates.RecordedSalePayment)
                .GroupBy(p => new
                {
                    p.PaymentMethodId,
                    p.PaymentMethodCode,
                    p.PaymentMethodName,
                    p.PaymentMethodNameAr,
                    LegacyMethod = !p.PaymentMethodId.HasValue
                        && (p.PaymentMethodCode == null || p.PaymentMethodCode == string.Empty)
                        && (p.PaymentMethodName == null || p.PaymentMethodName == string.Empty)
                        && (p.PaymentMethodNameAr == null || p.PaymentMethodNameAr == string.Empty)
                            ? p.Method
                            : null
                })
                .Select(g => new
                {
                    g.Key.PaymentMethodId,
                    g.Key.PaymentMethodCode,
                    g.Key.PaymentMethodName,
                    g.Key.PaymentMethodNameAr,
                    Method = g.Key.LegacyMethod,
                    OrderCount = g.Select(p => p.OrderId).Distinct().Count(),
                    CollectedRevenue = g.Sum(p => p.Amount),
                    GrossRevenue = g.Sum(p => p.Order.TotalAmount > 0 && p.Order.Subtotal > 0
                        ? p.Order.Subtotal * p.Amount / p.Order.TotalAmount
                        : p.Amount),
                    DiscountAmount = g.Sum(p => p.Order.TotalAmount > 0
                        ? p.Order.DiscountAmount * p.Amount / p.Order.TotalAmount
                        : 0m),
                    TotalCommission = g.Sum(p => p.CostSharingCommissionAmount),
                    RestaurantShare = g.Sum(p => p.CostSharingRestaurantShareAmount),
                    CounterpartyShare = g.Sum(p => p.CostSharingCounterpartyShareAmount)
                })
                .ToListAsync(ct);

            return rows.Select(r => new PaymentAggregateRow(
                r.PaymentMethodId,
                r.PaymentMethodCode,
                r.PaymentMethodName,
                r.PaymentMethodNameAr,
                r.Method,
                r.OrderCount,
                r.CollectedRevenue,
                r.GrossRevenue,
                r.DiscountAmount,
                r.TotalCommission,
                r.RestaurantShare,
                r.CounterpartyShare)).ToList();
        }

        private async Task<IReadOnlyList<LegacyPaymentAggregateRow>> QueryLegacyPaymentAggregatesAsync(
            CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => !o.Payments.Any())
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new
                {
                    Method = g.Key,
                    OrderCount = g.Count(),
                    CollectedRevenue = g.Sum(o => o.TotalAmount),
                    GrossRevenue = g.Sum(o => o.Subtotal > 0 ? o.Subtotal : o.TotalAmount),
                    DiscountAmount = g.Sum(o => o.DiscountAmount)
                })
                .ToListAsync(ct);

            return rows.Select(r => new LegacyPaymentAggregateRow(
                r.Method,
                r.OrderCount,
                r.CollectedRevenue,
                r.GrossRevenue,
                r.DiscountAmount)).ToList();
        }

        private async Task<IReadOnlyList<OrderSourceAggregateRow>> QueryOrderSourceAggregatesAsync(
            CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .GroupBy(o => new { o.OrderSource, o.OrderType })
                .Select(g => new
                {
                    g.Key.OrderSource,
                    g.Key.OrderType,
                    OrderCount = g.Count(),
                    GrossRevenue = g.Sum(o => o.Subtotal > 0 ? o.Subtotal : o.TotalAmount),
                    NetRevenue = g.Sum(o => o.NetRestaurantRevenue > 0
                        ? o.NetRestaurantRevenue
                        : o.TotalAmount)
                })
                .ToListAsync(ct);

            return rows.Select(r => new OrderSourceAggregateRow(
                r.OrderSource,
                r.OrderType,
                r.OrderCount,
                r.GrossRevenue,
                r.NetRevenue)).ToList();
        }

        private async Task<IReadOnlyList<PartnerAggregateRow>> QueryPartnerAggregatesAsync(
            CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.OrderSource == OrderSource.Talabat
                    || o.OrderSource == OrderSource.DeliveryPartner
                    || o.DeliveryPartnerId.HasValue)
                .GroupBy(o => new
                {
                    o.OrderSource,
                    o.DeliveryPartnerId,
                    o.DeliveryPartnerName,
                    o.DeliveryPartnerNameAr,
                    o.DeliveryPartnerCode
                })
                .Select(g => new
                {
                    g.Key.OrderSource,
                    g.Key.DeliveryPartnerId,
                    g.Key.DeliveryPartnerName,
                    g.Key.DeliveryPartnerNameAr,
                    g.Key.DeliveryPartnerCode,
                    OrderCount = g.Count(),
                    GrossRevenue = g.Sum(o => o.Subtotal > 0 ? o.Subtotal : o.TotalAmount),
                    NetRevenue = g.Sum(o => o.NetRestaurantRevenue > 0
                        ? o.NetRestaurantRevenue
                        : o.TotalAmount),
                    DeliveryFees = g.Sum(o => o.MarketplaceDeliveryFee > 0
                        ? o.MarketplaceDeliveryFee
                        : o.OrderSource == OrderSource.Talabat
                            ? o.TalabatDeliveryFee ?? 0m
                            : o.PartnerDeliveryFee ?? 0m),
                    PartnerFees = g.Sum(o => o.MarketplaceServiceFee > 0
                        ? o.MarketplaceServiceFee
                        : o.OrderSource == OrderSource.Talabat
                            ? o.TalabatServiceFee ?? 0m
                            : o.PartnerServiceFee ?? 0m),
                    TotalCommission = g.Sum(o => o.CostSharingTotalCommission),
                    RestaurantShare = g.Sum(o => o.CostSharingRestaurantShare),
                    CounterpartyShare = g.Sum(o => o.CostSharingCounterpartyShare),
                    NetSettlement = g.Sum(o => o.CostSharingCalculatedAt.HasValue
                        ? o.CostSharingNetSettlement
                        : o.TotalAmount),
                    NetRevenueAfterCostSharing = g.Sum(o =>
                        (o.NetRestaurantRevenue > 0m ? o.NetRestaurantRevenue : o.TotalAmount)
                        - o.CostSharingRestaurantShare)
                })
                .ToListAsync(ct);

            return rows.Select(r => new PartnerAggregateRow(
                r.OrderSource,
                r.DeliveryPartnerId,
                r.DeliveryPartnerName,
                r.DeliveryPartnerNameAr,
                r.DeliveryPartnerCode,
                r.OrderCount,
                r.GrossRevenue,
                r.NetRevenue,
                r.DeliveryFees,
                r.PartnerFees,
                r.TotalCommission,
                r.RestaurantShare,
                r.CounterpartyShare,
                r.NetSettlement,
                r.NetRevenueAfterCostSharing)).ToList();
        }

        private async Task<IReadOnlyList<PartnerSettlementAggregateRow>> QueryPartnerSettlementAggregatesAsync(
            CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.OrderSource == OrderSource.Talabat
                    || o.OrderSource == OrderSource.DeliveryPartner
                    || o.DeliveryPartnerId.HasValue)
                .GroupBy(o => new
                {
                    o.OrderSource,
                    o.DeliveryPartnerId,
                    o.DeliveryPartnerName,
                    o.DeliveryPartnerCode,
                    SettlementMethod = o.OrderSource == OrderSource.Talabat
                        ? o.TalabatPaymentMethod
                        : o.PartnerPaymentMethod ?? o.PaymentMethod
                })
                .Select(g => new
                {
                    g.Key.OrderSource,
                    g.Key.DeliveryPartnerId,
                    g.Key.DeliveryPartnerName,
                    g.Key.DeliveryPartnerCode,
                    g.Key.SettlementMethod,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .ToListAsync(ct);

            return rows.Select(r => new PartnerSettlementAggregateRow(
                r.OrderSource,
                r.DeliveryPartnerId,
                r.DeliveryPartnerName,
                r.DeliveryPartnerCode,
                r.SettlementMethod,
                r.OrderCount,
                r.Revenue)).ToList();
        }

        private async Task<IReadOnlyList<RefundAggregateRow>> QueryRefundAggregatesAsync(
            CancellationToken ct)
        {
            var rows = await _q.RefundLogs()
                .GroupBy(r => new
                {
                    PaymentMethod = r.OriginalPaymentMethod ?? r.Order.PaymentMethod,
                    r.Order.OrderSource,
                    r.Order.OrderType,
                    r.Order.DeliveryPartnerId,
                    r.Order.DeliveryPartnerName,
                    r.Order.DeliveryPartnerCode
                })
                .Select(g => new
                {
                    g.Key.PaymentMethod,
                    g.Key.OrderSource,
                    g.Key.OrderType,
                    g.Key.DeliveryPartnerId,
                    g.Key.DeliveryPartnerName,
                    g.Key.DeliveryPartnerCode,
                    OrderCount = g.Select(r => r.OrderId).Distinct().Count(),
                    Amount = g.Sum(r => r.RefundAmount)
                })
                .ToListAsync(ct);

            return rows.Select(r => new RefundAggregateRow(
                r.PaymentMethod,
                r.OrderSource,
                r.OrderType,
                r.DeliveryPartnerId,
                r.DeliveryPartnerName,
                r.DeliveryPartnerCode,
                r.OrderCount,
                r.Amount)).ToList();
        }

        private async Task<IReadOnlyList<CancellationAggregateRow>> QueryCancellationAggregatesAsync(
            CancellationToken ct)
        {
            var rows = await CancelledBeforePaymentLogs()
                .GroupBy(c => new { c.OrderId, c.Order.OrderSource, c.Order.OrderType })
                .Select(g => new
                {
                    g.Key.OrderSource,
                    g.Key.OrderType,
                    Amount = g.Sum(c => c.Amount)
                })
                .ToListAsync(ct);

            return rows
                .GroupBy(row => new { row.OrderSource, row.OrderType })
                .Select(group => new CancellationAggregateRow(
                    group.Key.OrderSource,
                    group.Key.OrderType,
                    group.Count(),
                    group.Sum(row => row.Amount)))
                .ToList();
        }

        private async Task<IReadOnlyList<PaymentMethodMetadata>> LoadPaymentMetadataAsync(
            IReadOnlyList<PaymentAggregateRow> payments,
            CancellationToken ct)
        {
            var current = await _db.PaymentMethods.AsNoTracking()
                .Select(m => new PaymentMethodMetadata(
                    m.Id, m.Code, m.NameEn, m.NameAr, m.IsActive))
                .ToListAsync(ct);
            var ids = payments
                .Where(p => p.PaymentMethodId.HasValue)
                .Select(p => p.PaymentMethodId!.Value)
                .Except(current.Select(m => m.Id))
                .ToList();
            if (ids.Count == 0) return current;

            var deleted = await _db.PaymentMethods.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => ids.Contains(m.Id))
                .Select(m => new PaymentMethodMetadata(
                    m.Id, m.Code, m.NameEn, m.NameAr, false))
                .ToListAsync(ct);
            return current.Concat(deleted).ToList();
        }

        private async Task<IReadOnlyList<PartnerMetadata>> LoadPartnerMetadataAsync(
            IReadOnlyList<PartnerAggregateRow> partners,
            CancellationToken ct)
        {
            var current = await _db.DeliveryPartners.AsNoTracking()
                .Select(p => new PartnerMetadata(
                    p.Id, p.Code, p.Name, p.NameAr,
                    p.Status == DeliveryPartnerStatus.Active))
                .ToListAsync(ct);
            var ids = partners
                .Where(p => p.DeliveryPartnerId.HasValue)
                .Select(p => p.DeliveryPartnerId!.Value)
                .Except(current.Select(p => p.Id))
                .ToList();
            if (ids.Count == 0) return current;

            var deleted = await _db.DeliveryPartners.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Select(p => new PartnerMetadata(p.Id, p.Code, p.Name, p.NameAr, false))
                .ToListAsync(ct);
            return current.Concat(deleted).ToList();
        }

        private static IReadOnlyList<PaymentMethodAnalyticsDto> BuildPaymentMethodAnalytics(
            IReadOnlyList<PaymentAggregateRow> payments,
            IReadOnlyList<LegacyPaymentAggregateRow> legacyPayments,
            IReadOnlyList<RefundAggregateRow> refunds,
            IReadOnlyList<PaymentMethodMetadata> metadata)
        {
            var buckets = new Dictionary<string, PaymentAnalyticsAccumulator>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var payment in payments)
            {
                var descriptor = ResolvePaymentDescriptor(payment, metadata);
                AddPaymentAggregate(buckets, descriptor, payment.OrderCount,
                    payment.CollectedRevenue, payment.GrossRevenue, payment.DiscountAmount,
                    payment.TotalCommission, payment.RestaurantShare, payment.CounterpartyShare);
            }
            foreach (var payment in legacyPayments)
            {
                var legacy = OrderPaymentHelper.ResolveLegacyBucket(payment.Method);
                var descriptor = new PaymentDescriptor(
                    legacy.Key, legacy.Label, legacy.LabelAr, false);
                AddPaymentAggregate(buckets, descriptor, payment.OrderCount,
                    payment.CollectedRevenue, payment.GrossRevenue, payment.DiscountAmount,
                    0m, 0m, 0m);
            }
            foreach (var refund in refunds)
                AddPaymentRefund(
                    buckets, refund.PaymentMethod, refund.OrderCount, refund.Amount);

            var totalGross = buckets.Values.Sum(b => b.GrossRevenue);
            return buckets.Values
                .OrderByDescending(b => b.GrossRevenue)
                .Select(b => new PaymentMethodAnalyticsDto
                {
                    Key = b.Key,
                    Name = b.Name,
                    NameAr = b.NameAr,
                    IsActive = b.IsActive,
                    TotalOrders = b.OrderCount,
                    GrossRevenue = Math.Round(b.GrossRevenue, 2),
                    RefundedOrderCount = b.RefundedOrderCount,
                    RefundAmount = Math.Round(b.RefundAmount, 2),
                    DiscountAmount = Math.Round(b.DiscountAmount, 2),
                    NetRevenue = Math.Round(b.CollectedRevenue - b.RefundAmount, 2),
                    TotalCommission = Math.Round(b.TotalCommission, 2),
                    RestaurantShare = Math.Round(b.RestaurantShare, 2),
                    CounterpartyShare = Math.Round(b.CounterpartyShare, 2),
                    NetSettlement = Math.Round(
                        b.CollectedRevenue - b.RefundAmount - b.TotalCommission, 2),
                    NetRevenueAfterCostSharing = Math.Round(
                        b.CollectedRevenue - b.RefundAmount - b.RestaurantShare, 2),
                    AverageOrderValue = b.OrderCount == 0
                        ? 0
                        : Math.Round(b.GrossRevenue / b.OrderCount, 2),
                    RevenuePercentage = totalGross == 0
                        ? 0
                        : Math.Round(b.GrossRevenue / totalGross * 100m, 2)
                })
                .ToList();
        }

        private static PaymentDescriptor ResolvePaymentDescriptor(
            PaymentAggregateRow payment,
            IReadOnlyList<PaymentMethodMetadata> metadata)
        {
            var configured = payment.PaymentMethodId.HasValue
                ? metadata.FirstOrDefault(m => m.Id == payment.PaymentMethodId.Value)
                : metadata.FirstOrDefault(m =>
                    !string.IsNullOrWhiteSpace(payment.PaymentMethodCode)
                    && string.Equals(m.Code, payment.PaymentMethodCode, StringComparison.OrdinalIgnoreCase));
            if (configured is not null)
                return new PaymentDescriptor(
                    configured.Code,
                    configured.Name,
                    configured.NameAr,
                    configured.IsActive);

            if (!string.IsNullOrWhiteSpace(payment.PaymentMethodCode)
                || !string.IsNullOrWhiteSpace(payment.PaymentMethodName))
            {
                var key = FirstNotBlank(payment.PaymentMethodCode, payment.PaymentMethodName)!;
                return new PaymentDescriptor(
                    key,
                    FirstNotBlank(payment.PaymentMethodName, payment.PaymentMethodCode)!,
                    payment.PaymentMethodNameAr,
                    false);
            }

            var legacy = OrderPaymentHelper.ResolveLegacyBucket(payment.Method);
            return new PaymentDescriptor(legacy.Key, legacy.Label, legacy.LabelAr, false);
        }

        private static void AddPaymentAggregate(
            IDictionary<string, PaymentAnalyticsAccumulator> buckets,
            PaymentDescriptor descriptor,
            int orderCount,
            decimal collectedRevenue,
            decimal grossRevenue,
            decimal discountAmount,
            decimal totalCommission,
            decimal restaurantShare,
            decimal counterpartyShare)
        {
            if (!buckets.TryGetValue(descriptor.Key, out var bucket))
            {
                bucket = new PaymentAnalyticsAccumulator(descriptor);
                buckets[descriptor.Key] = bucket;
            }
            bucket.Add(orderCount, collectedRevenue, grossRevenue, discountAmount,
                totalCommission, restaurantShare, counterpartyShare);
        }

        private static void AddPaymentRefund(
            IDictionary<string, PaymentAnalyticsAccumulator> buckets,
            string? method,
            int orderCount,
            decimal amount)
        {
            var match = buckets.Values.FirstOrDefault(b => b.Matches(method));
            if (match is null)
            {
                var legacy = OrderPaymentHelper.ResolveLegacyBucket(method);
                if (!buckets.TryGetValue(legacy.Key, out match))
                {
                    var descriptor = new PaymentDescriptor(
                        legacy.Key, legacy.Label, legacy.LabelAr, false);
                    match = new PaymentAnalyticsAccumulator(descriptor);
                    buckets[descriptor.Key] = match;
                }
            }
            match.RefundedOrderCount += orderCount;
            match.RefundAmount += amount;
        }

        private static IReadOnlyList<OrderSourceAnalyticsDto> BuildOrderSourceAnalytics(
            IReadOnlyList<OrderSourceAggregateRow> rows,
            IReadOnlyList<RefundAggregateRow> refunds,
            IReadOnlyList<CancellationAggregateRow> cancellations)
        {
            var buckets = new Dictionary<string, OrderSourceAccumulator>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var descriptor = ResolveOrderSource(row.OrderSource, row.OrderType);
                if (!buckets.TryGetValue(descriptor.Key, out var bucket))
                {
                    bucket = new OrderSourceAccumulator(descriptor);
                    buckets[descriptor.Key] = bucket;
                }
                bucket.Add(row.OrderCount, row.GrossRevenue, row.NetRevenue);
            }
            foreach (var refund in refunds)
            {
                var descriptor = ResolveOrderSource(refund.OrderSource, refund.OrderType);
                if (!buckets.TryGetValue(descriptor.Key, out var bucket))
                {
                    bucket = new OrderSourceAccumulator(descriptor);
                    buckets[descriptor.Key] = bucket;
                }
                bucket.RefundedOrderCount += refund.OrderCount;
                bucket.RefundAmount += refund.Amount;
            }
            foreach (var cancellation in cancellations)
            {
                var descriptor = ResolveOrderSource(
                    cancellation.OrderSource, cancellation.OrderType);
                if (!buckets.TryGetValue(descriptor.Key, out var bucket))
                {
                    bucket = new OrderSourceAccumulator(descriptor);
                    buckets[descriptor.Key] = bucket;
                }
                bucket.CancelledBeforePaymentCount += cancellation.OrderCount;
                bucket.CancelledBeforePaymentAmount += cancellation.Amount;
            }

            return buckets.Values
                .OrderByDescending(b => b.GrossRevenue)
                .Select(b => new OrderSourceAnalyticsDto
                {
                    Key = b.Key,
                    Name = b.Name,
                    NameAr = b.NameAr,
                    OrderCount = b.OrderCount,
                    GrossRevenue = Math.Round(b.GrossRevenue, 2),
                    NetRevenue = Math.Round(b.NetRevenue - b.RefundAmount, 2),
                    RefundedOrderCount = b.RefundedOrderCount,
                    RefundAmount = Math.Round(b.RefundAmount, 2),
                    CancelledBeforePaymentCount = b.CancelledBeforePaymentCount,
                    CancelledBeforePaymentAmount =
                        Math.Round(b.CancelledBeforePaymentAmount, 2),
                    AverageOrderValue = b.OrderCount == 0
                        ? 0
                        : Math.Round(b.GrossRevenue / b.OrderCount, 2)
                })
                .ToList();
        }

        private static OrderSourceDescriptor ResolveOrderSource(
            OrderSource source,
            OrderType type)
        {
            if (source is OrderSource.Talabat or OrderSource.DeliveryPartner)
                return new OrderSourceDescriptor("PartnerOrders", "Partner orders", "طلبات الشركاء");
            if (type == OrderType.DineIn)
                return new OrderSourceDescriptor("DineIn", "Dine in", "محلي");
            if (type == OrderType.Delivery)
                return new OrderSourceDescriptor("Delivery", "Delivery", "توصيل");
            if (source == OrderSource.Mobile)
                return new OrderSourceDescriptor("Pickup", "Pickup", "استلام");
            return new OrderSourceDescriptor("TakeAway", "Take away", "سفري");
        }

        private static IReadOnlyList<PartnerAnalyticsDto> BuildPartnerAnalytics(
            IReadOnlyList<PartnerAggregateRow> rows,
            IReadOnlyList<PartnerSettlementAggregateRow> settlements,
            IReadOnlyList<RefundAggregateRow> refunds,
            IReadOnlyList<PartnerMetadata> metadata)
        {
            var buckets = new Dictionary<string, PartnerAnalyticsAccumulator>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var descriptor = ResolvePartnerDescriptor(
                    row.OrderSource, row.DeliveryPartnerId, row.DeliveryPartnerCode,
                    row.DeliveryPartnerName, row.DeliveryPartnerNameAr, metadata);
                var bucket = GetPartnerBucket(buckets, descriptor);
                bucket.Add(row);
            }
            foreach (var settlement in settlements)
            {
                var descriptor = ResolvePartnerDescriptor(
                    settlement.OrderSource, settlement.DeliveryPartnerId,
                    settlement.DeliveryPartnerCode, settlement.DeliveryPartnerName, null, metadata);
                GetPartnerBucket(buckets, descriptor).AddSettlement(settlement);
            }
            foreach (var refund in refunds.Where(IsPartnerRefund))
            {
                var descriptor = ResolvePartnerDescriptor(
                    refund.OrderSource, refund.DeliveryPartnerId,
                    refund.DeliveryPartnerCode, refund.DeliveryPartnerName, null, metadata);
                GetPartnerBucket(buckets, descriptor).RefundAmount += refund.Amount;
            }

            return buckets.Values
                .OrderByDescending(b => b.GrossRevenue)
                .Select(b => b.ToDto())
                .ToList();
        }

        private static PartnerAnalyticsAccumulator GetPartnerBucket(
            IDictionary<string, PartnerAnalyticsAccumulator> buckets,
            PartnerDescriptor descriptor)
        {
            if (buckets.TryGetValue(descriptor.Key, out var bucket)) return bucket;
            bucket = new PartnerAnalyticsAccumulator(descriptor);
            buckets[descriptor.Key] = bucket;
            return bucket;
        }

        private static PartnerDescriptor ResolvePartnerDescriptor(
            OrderSource source,
            Guid? id,
            string? code,
            string? name,
            string? nameAr,
            IReadOnlyList<PartnerMetadata> metadata)
        {
            if (source == OrderSource.Talabat)
                return new PartnerDescriptor("TALABAT", "Talabat", "طلبات", true);

            var configured = id.HasValue
                ? metadata.FirstOrDefault(p => p.Id == id.Value)
                : metadata.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(code)
                    && string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
            if (configured is not null)
                return new PartnerDescriptor(
                    $"ID:{configured.Id:N}",
                    configured.Name,
                    configured.NameAr,
                    configured.IsActive);

            var key = !string.IsNullOrWhiteSpace(code)
                ? $"CODE:{code.Trim()}"
                : $"NAME:{FirstNotBlank(name, "Delivery Partner")}";
            return new PartnerDescriptor(
                key,
                FirstNotBlank(name, code, "Delivery Partner")!,
                nameAr,
                false);
        }

        private static bool IsPartnerRefund(RefundAggregateRow row)
            => row.OrderSource is OrderSource.Talabat or OrderSource.DeliveryPartner
                || row.DeliveryPartnerId.HasValue;

        private static IReadOnlyList<CategorySliceDto> BuildCardBreakdown(
            IReadOnlyList<PaymentMethodAnalyticsDto> rows)
        {
            var groups = rows
                .Select(row => (Row: row, Category: ResolveCardCategory(row)))
                .Where(item => item.Category is not null)
                .GroupBy(item => item.Category!.Value.Key)
                .Select(group => new BreakdownAggregate(
                    group.Key,
                    group.First().Category!.Value.Name,
                    group.First().Category!.Value.NameAr,
                    group.Sum(item => item.Row.GrossRevenue),
                    group.Sum(item => item.Row.TotalOrders)))
                .OrderByDescending(group => group.Value)
                .ToList();
            return BuildCategoryBreakdown(groups);
        }

        private static (string Key, string Name, string NameAr)? ResolveCardCategory(
            PaymentMethodAnalyticsDto row)
        {
            var value = $"{row.Key} {row.Name}".ToUpperInvariant();
            if (value.Contains("PAYMOB")) return null;
            if (value.Contains("VISA") && value.Contains("MASTER"))
                return ("VISA_MASTERCARD", "Visa / Mastercard", "فيزا / ماستركارد");
            if (value.Contains("VISA")) return ("VISA", "Visa", "فيزا");
            if (value.Contains("MASTER")) return ("MASTERCARD", "Mastercard", "ماستركارد");
            if (value.Contains("MEEZA")) return ("MEEZA", "Meeza", "ميزة");
            if (value.Contains("AMEX") || value.Contains("AMERICAN EXPRESS"))
                return ("AMEX", "Amex", "أمريكان إكسبريس");
            if (value.Contains("CARD") || value.Contains("LEGACY_CARD"))
                return ("OTHER", "Other cards", "بطاقات أخرى");
            return null;
        }

        private static IReadOnlyList<CategorySliceDto> BuildPayMobBreakdown(
            IReadOnlyList<PaymentMethodAnalyticsDto> rows)
        {
            var groups = rows
                .Where(row => $"{row.Key} {row.Name}".Contains("PAYMOB", StringComparison.OrdinalIgnoreCase))
                .GroupBy(ResolvePayMobCategory)
                .Select(group => new BreakdownAggregate(
                    group.Key.Key,
                    group.Key.Name,
                    group.Key.NameAr,
                    group.Sum(row => row.GrossRevenue),
                    group.Sum(row => row.TotalOrders)))
                .OrderByDescending(group => group.Value)
                .ToList();
            return BuildCategoryBreakdown(groups);
        }

        private static (string Key, string Name, string NameAr) ResolvePayMobCategory(
            PaymentMethodAnalyticsDto row)
        {
            var value = $"{row.Key} {row.Name}".ToUpperInvariant();
            if (value.Contains("WALLET")) return ("WALLET", "Wallet", "محفظة");
            if (value.Contains("INSTAPAY")) return ("INSTAPAY", "InstaPay", "إنستاباي");
            if (value.Contains("BANK")) return ("BANK", "Bank transfer", "تحويل بنكي");
            if (value.Contains("CARD") || value.Contains("VISA") || value.Contains("MASTER"))
                return ("CARD", "Card", "بطاقة");
            return ("PAYMOB", "PayMob", "باي موب");
        }

        private static IReadOnlyList<CategorySliceDto> BuildCategoryBreakdown(
            IReadOnlyList<BreakdownAggregate> rows)
        {
            var total = rows.Sum(row => row.Value);
            return rows.Select(row => new CategorySliceDto(
                    row.Name,
                    row.NameAr,
                    Math.Round(row.Value, 2),
                    total == 0 ? 0 : Math.Round(row.Value / total * 100m, 2),
                    row.Count))
                .ToList();
        }

        private static bool IsCashSettlement(string? method)
        {
            if (string.IsNullOrWhiteSpace(method)) return false;
            var normalized = method.Trim().ToLowerInvariant();
            return normalized.Contains("cash")
                || normalized.Contains("nakit")
                || normalized.Contains("نقد");
        }

        private static string? FirstNotBlank(params string?[] values)
            => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

        private async Task<CancellationSummaryDto> ComputeCancellationSummaryAsync(
            CancellationToken ct)
        {
            var cancelled = await CancelledBeforePaymentLogs()
                .GroupBy(c => c.OrderId)
                .Select(g => new { Amount = g.Sum(c => c.Amount) })
                .ToListAsync(ct);

            var refunds = await _q.RefundLogs()
                .GroupBy(r => r.OrderId)
                .Select(g => new { Amount = g.Sum(r => r.RefundAmount) })
                .ToListAsync(ct);

            return new CancellationSummaryDto
            {
                CancelledBeforePaymentCount = cancelled.Count,
                CancelledBeforePaymentAmount = Math.Round(cancelled.Sum(row => row.Amount), 2),
                RefundedOrderCount = refunds.Count,
                RefundedAmount = Math.Round(refunds.Sum(row => row.Amount), 2)
            };
        }

        private async Task<IReadOnlyList<CategoryRevenueDto>> ComputeCategoryRevenueAsync(CancellationToken ct)
        {
            var rows = await _q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .GroupBy(oi => oi.Product.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    Revenue    = g.Sum(oi => oi.Price * oi.Quantity),
                    Cost       = g.Sum(oi => oi.StockDeductedCost),
                    Quantity   = g.Sum(oi => oi.Quantity),
                    OrderCount = g.Select(oi => oi.OrderId).Distinct().Count()
                })
                .ToListAsync(ct);

            var categoryIds = rows
                .Where(r => r.CategoryId.HasValue)
                .Select(r => r.CategoryId!.Value)
                .ToList();

            var categoryNames = await _db.Categories
                .AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.NameAr })
                .ToDictionaryAsync(c => c.Id, ct);

            var total = rows.Sum(r => r.Revenue);
            return rows
                .Select(r =>
                {
                    categoryNames.TryGetValue(r.CategoryId ?? Guid.Empty, out var cat);
                    var profit = r.Revenue - r.Cost;
                    return new CategoryRevenueDto
                    {
                        CategoryId        = r.CategoryId,
                        Name              = cat?.Name ?? "Uncategorized",
                        NameAr            = cat?.NameAr,
                        Revenue           = Math.Round(r.Revenue, 2),
                        Cost              = Math.Round(r.Cost, 2),
                        Profit            = Math.Round(profit, 2),
                        MarginPercent     = r.Revenue == 0 ? 0 : Math.Round(profit / r.Revenue * 100m, 2),
                        Quantity          = r.Quantity,
                        OrderCount        = r.OrderCount,
                        RevenuePercentage = total == 0 ? 0 : Math.Round(r.Revenue / total * 100m, 2)
                    };
                })
                .OrderByDescending(r => r.Revenue)
                .ToList();
        }

        private async Task<CustomerRevenueDto> ComputeCustomerRevenueAsync(CancellationToken ct)
        {
            var orders = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Select(o => new { o.Id, o.CustomerId, o.TotalAmount })
                .ToListAsync(ct);

            var orderIds = orders.Select(o => o.Id).ToList();
            var costByOrder = await _q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .Where(oi => orderIds.Contains(oi.OrderId))
                .GroupBy(oi => oi.OrderId)
                .Select(g => new { OrderId = g.Key, Cost = g.Sum(oi => oi.StockDeductedCost) })
                .ToDictionaryAsync(r => r.OrderId, r => r.Cost, ct);

            var identified = orders.Where(o => o.CustomerId.HasValue).ToList();
            if (identified.Count == 0)
                return new CustomerRevenueDto
                {
                    RevenueFromAnonymous = Math.Round(orders.Sum(o => o.TotalAmount), 2)
                };

            var uniqueIds = identified.Select(o => o.CustomerId!.Value).Distinct().ToList();

            // Find customers whose first-ever sale predates the window → returning.
            var returningIds = await _q.AllOrders()
                .Where(o => o.CustomerId.HasValue && uniqueIds.Contains(o.CustomerId!.Value))
                .Where(OrderLifecyclePredicates.RecordedSale)
                .GroupBy(o => o.CustomerId)
                .Where(g => g.Min(o => o.PaidAt ?? o.CreatedAt) < _ctx.Window.StartUtc)
                .Select(g => g.Key!.Value)
                .ToListAsync(ct);

            var revenuePerCustomer = identified
                .GroupBy(o => o.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));

            var revenueFromIdentified = revenuePerCustomer.Values.Sum();
            var revenueFromAnonymous  = orders.Where(o => !o.CustomerId.HasValue).Sum(o => o.TotalAmount);
            var profitFromIdentified  = identified.Sum(o => o.TotalAmount - (costByOrder.TryGetValue(o.Id, out var cost) ? cost : 0m));
            var avgProfit             = uniqueIds.Count == 0
                ? 0m
                : Math.Round(profitFromIdentified / uniqueIds.Count, 2);
            var avgSpend              = uniqueIds.Count == 0
                ? 0m
                : Math.Round(revenueFromIdentified / uniqueIds.Count, 2);
            var avgFrequency          = uniqueIds.Count == 0
                ? 0m
                : Math.Round((decimal)identified.Count / uniqueIds.Count, 2);

            var topIds = revenuePerCustomer
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .Select(kv => kv.Key)
                .ToList();

            var customerMeta = await _db.Customers
                .AsNoTracking()
                .Where(c => topIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.NameAr, c.PhoneNumber })
                .ToDictionaryAsync(c => c.Id, ct);

            var orderCountPerCustomer = identified
                .GroupBy(o => o.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var topCustomers = topIds
                .Select(id =>
                {
                    customerMeta.TryGetValue(id, out var meta);
                    var name = meta?.Name ?? meta?.PhoneNumber ?? id.ToString()[..8];
                    orderCountPerCustomer.TryGetValue(id, out var cnt);
                    revenuePerCustomer.TryGetValue(id, out var rev);
                    return new RankedRowDto(id, name, meta?.NameAr, Math.Round(rev, 2), null, cnt);
                })
                .ToList();

            return new CustomerRevenueDto
            {
                UniqueCustomers        = uniqueIds.Count,
                NewCustomers           = uniqueIds.Count - returningIds.Count,
                ReturningCustomers     = returningIds.Count,
                RevenueFromIdentified  = Math.Round(revenueFromIdentified, 2),
                RevenueFromAnonymous   = Math.Round(revenueFromAnonymous, 2),
                AverageCustomerSpend   = avgSpend,
                AveragePurchaseFrequency = avgFrequency,
                ProfitFromIdentified   = Math.Round(profitFromIdentified, 2),
                AverageCustomerProfit  = avgProfit,
                TopCustomers           = topCustomers
            };
        }

        private async Task<FoodCostDto> ComputeFoodCostAsync(RevenueBreakdownDto revenue, CancellationToken ct)
        {
            var actualCost = await SumRecordedSaleCogsAsync(_q, ct);

            // Theoretical cost: for each sold unit, sum recipe ingredient amounts × raw material cost.
            var productRecipeCosts = await _db.RecipeItems
                .AsNoTracking()
                .GroupBy(ri => ri.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    UnitCost  = g.Sum(ri => ri.Amount * ri.RawMaterial.CostPerUnit)
                })
                .ToListAsync(ct);

            var hasTheoreticalData = productRecipeCosts.Count > 0;
            decimal theoreticalCost = 0m;

            if (hasTheoreticalData)
            {
                var recipeLookup = productRecipeCosts.ToDictionary(r => r.ProductId, r => r.UnitCost);

                var soldItems = await _q.OrderItems()
                    .Where(OrderLifecyclePredicates.RecordedSaleItem)
                    .Where(oi => !oi.IsComplimentary)
                    .Select(oi => new { oi.ProductId, oi.Quantity })
                    .ToListAsync(ct);

                theoreticalCost = soldItems.Sum(oi =>
                    oi.Quantity * (recipeLookup.TryGetValue(oi.ProductId, out var unitCost) ? unitCost : 0m));
            }

            var net                      = revenue.Net;
            var actualPct                = net == 0 ? 0m : Math.Round(actualCost / net * 100m, 2);
            var theoreticalPct           = net == 0 ? 0m : Math.Round(theoreticalCost / net * 100m, 2);
            var variance                 = Math.Round(actualCost - theoreticalCost, 2);
            var variancePct              = theoreticalCost == 0m ? 0m : Math.Round(variance / theoreticalCost * 100m, 2);

            return new FoodCostDto
            {
                ActualFoodCost                   = Math.Round(actualCost, 2),
                TheoreticalFoodCost              = Math.Round(theoreticalCost, 2),
                FoodCostPercentage               = actualPct,
                TheoreticalFoodCostPercentage    = theoreticalPct,
                FoodCostVariance                 = variance,
                FoodCostVariancePercentage       = variancePct,
                HasTheoreticalData               = hasTheoreticalData
            };
        }

        private async Task<PurchasingDto> ComputePurchasingAsync(CancellationToken ct)
        {
            var w = _ctx.Window;
            var query = _db.PurchaseOrders
                .AsNoTracking()
                .Where(po => po.Status == PurchaseOrderStatus.Received || po.Status == PurchaseOrderStatus.Approved)
                .Where(po => (po.ReceivedDate ?? po.CreatedAt) >= w.StartUtc
                          && (po.ReceivedDate ?? po.CreatedAt) < w.EndUtc);
            if (_ctx.Filter.BranchId.HasValue)
                query = query.Where(po => po.BranchId == _ctx.Filter.BranchId.Value);

            var pos = await query
                .Select(po => new
                {
                    po.SupplierId,
                    SupplierName   = po.Supplier.Name,
                    SupplierNameAr = po.Supplier.NameAr,
                    DateBucket     = po.ReceivedDate ?? po.CreatedAt,
                    po.CreatedAt,
                    po.ExpectedDate,
                    po.ReceivedDate,
                    po.TotalAmount
                })
                .ToListAsync(ct);

            if (pos.Count == 0)
                return new PurchasingDto();

            var totalCost = pos.Sum(po => po.TotalAmount);

            var supplierSpend = pos
                .GroupBy(po => new { po.SupplierId, po.SupplierName, po.SupplierNameAr })
                .Select(g =>
                {
                    var spend = g.Sum(po => po.TotalAmount);
                    var received = g.Where(po => po.ReceivedDate.HasValue).ToList();
                    var avgLead = received.Count == 0
                        ? 0m
                        : Math.Round((decimal)received.Average(po => (po.ReceivedDate!.Value - po.CreatedAt).TotalDays), 2);
                    var onTime = received.Count == 0
                        ? 0m
                        : Math.Round((decimal)received.Count(po => po.ReceivedDate!.Value <= po.ExpectedDate) / received.Count * 100m, 2);
                    return new SupplierSpendDto
                    {
                        SupplierId      = g.Key.SupplierId,
                        Name            = g.Key.SupplierName,
                        NameAr          = g.Key.SupplierNameAr,
                        TotalSpend      = Math.Round(spend, 2),
                        OrderCount      = g.Count(),
                        SpendPercentage = totalCost == 0m ? 0m : Math.Round(spend / totalCost * 100m, 2),
                        AverageLeadTimeDays = avgLead,
                        OnTimeDeliveryPercentage = onTime,
                        ReceivedOrderCount = received.Count
                    };
                })
                .OrderByDescending(s => s.TotalSpend)
                .ToList();

            var purchaseTrend = pos
                .GroupBy(po => new DateTime(
                    po.DateBucket.Year, po.DateBucket.Month, po.DateBucket.Day,
                    0, 0, 0, DateTimeKind.Utc))
                .Select(g => new TimeSeriesPointDto(g.Key, Math.Round(g.Sum(po => po.TotalAmount), 2), g.Count()))
                .OrderBy(p => p.BucketStartUtc)
                .ToList();

            return new PurchasingDto
            {
                TotalPurchasingCost = Math.Round(totalCost, 2),
                PurchaseOrderCount  = pos.Count,
                SupplierSpend       = supplierSpend,
                PurchaseTrend       = purchaseTrend
            };
        }

        private async Task<IReadOnlyList<ProductProfitabilityDto>> ComputeProductProfitabilityAsync(CancellationToken ct)
        {
            var rows = await _q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .GroupBy(oi => new { oi.ProductId, oi.ProductName, ProductNameAr = oi.Product.NameAr })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.ProductNameAr,
                    Revenue  = g.Sum(oi => oi.Price * oi.Quantity),
                    Cost     = g.Sum(oi => oi.StockDeductedCost),
                    Quantity = g.Sum(oi => oi.Quantity)
                })
                .ToListAsync(ct);

            return rows
                .Select(r =>
                {
                    var profit = r.Revenue - r.Cost;
                    return new ProductProfitabilityDto
                    {
                        ProductId     = r.ProductId,
                        Name          = r.ProductName,
                        NameAr        = r.ProductNameAr,
                        Revenue       = Math.Round(r.Revenue, 2),
                        Cost          = Math.Round(r.Cost, 2),
                        Profit        = Math.Round(profit, 2),
                        MarginPercent = r.Revenue == 0 ? 0 : Math.Round(profit / r.Revenue * 100m, 2),
                        Quantity      = r.Quantity
                    };
                })
                .OrderByDescending(r => r.Profit)
                .Take(15)
                .ToList();
        }

        private async Task<LaborCostDto> ComputeLaborCostAsync(RevenueBreakdownDto revenue, CancellationToken ct)
        {
            var agg = await _q.TimeEntries()
                .GroupBy(te => 1)
                .Select(g => new
                {
                    Cost    = g.Sum(te => te.TotalCost),
                    Minutes = g.Sum(te => te.DurationMinutes ?? 0),
                    Shifts  = g.Count()
                })
                .FirstOrDefaultAsync(ct);

            if (agg is null || agg.Shifts == 0) return new LaborCostDto();

            var net   = revenue.Net;
            var hours = Math.Round(agg.Minutes / 60m, 2);
            return new LaborCostDto
            {
                LaborCost           = Math.Round(agg.Cost, 2),
                LaborCostPercentage = net == 0 ? 0 : Math.Round(agg.Cost / net * 100m, 2),
                LaborHours          = hours,
                RevenuePerLaborHour = hours == 0 ? 0 : Math.Round(net / hours, 2),
                ShiftCount          = agg.Shifts,
                HasLaborData        = true
            };
        }

        private async Task<DiscountCostDto> ComputeDiscountCostAsync(RevenueBreakdownDto revenue, CancellationToken ct)
        {
            var orderDisc = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .GroupBy(o => 1)
                .Select(g => new
                {
                    Manual  = g.Sum(o => o.DiscountAmount),
                    Voucher = g.Sum(o => o.VoucherDiscountAmount)
                })
                .FirstOrDefaultAsync(ct);

            var itemDisc = await _q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .GroupBy(oi => 1)
                .Select(g => new
                {
                    Complimentary = g.Sum(oi => oi.IsComplimentary ? oi.Price * oi.Quantity : 0m),
                    Partner = g.Sum(oi => oi.HasPartnerDiscountOverride
                        ? ((oi.PartnerOriginalUnitPrice ?? 0m) - (oi.PartnerDiscountedUnitPrice ?? 0m)) * oi.Quantity
                        : 0m)
                })
                .FirstOrDefaultAsync(ct);

            var manual  = orderDisc?.Manual ?? 0m;
            var voucher = orderDisc?.Voucher ?? 0m;
            var comp    = itemDisc?.Complimentary ?? 0m;
            var partner = itemDisc?.Partner ?? 0m;
            var total   = manual + voucher + comp + partner;

            return new DiscountCostDto
            {
                ManualDiscounts    = Math.Round(manual, 2),
                VoucherDiscounts   = Math.Round(voucher, 2),
                ComplimentaryValue = Math.Round(comp, 2),
                PartnerDiscounts   = Math.Round(partner, 2),
                TotalDiscountCost  = Math.Round(total, 2),
                DiscountPercentage = revenue.Gross == 0 ? 0 : Math.Round(total / revenue.Gross * 100m, 2)
            };
        }

        private async Task<SalesMixDto> ComputeSalesMixAsync(RevenueBreakdownDto revenue, CancellationToken ct)
        {
            var agg = await _q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .GroupBy(oi => 1)
                .Select(g => new
                {
                    TotalItems = g.Sum(oi => oi.Quantity),
                    CompCount  = g.Sum(oi => oi.IsComplimentary ? oi.Quantity : 0),
                    CompValue  = g.Sum(oi => oi.IsComplimentary ? oi.Price * oi.Quantity : 0m)
                })
                .FirstOrDefaultAsync(ct);

            var modifierRevenue = await _q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .SelectMany(oi => oi.Modifiers.Select(m => new { m.Price, m.Quantity, ItemQty = oi.Quantity }))
                .SumAsync(x => (decimal?)(x.Price * x.Quantity * x.ItemQty), ct) ?? 0m;

            var orders = revenue.OrderCount;
            return new SalesMixDto
            {
                TotalItems             = agg?.TotalItems ?? 0,
                AverageItemsPerOrder   = orders == 0 ? 0 : Math.Round((decimal)(agg?.TotalItems ?? 0) / orders, 2),
                ModifierRevenue        = Math.Round(modifierRevenue, 2),
                ComplimentaryItemCount = agg?.CompCount ?? 0,
                ComplimentaryValue     = Math.Round(agg?.CompValue ?? 0m, 2)
            };
        }

        private async Task<IReadOnlyList<RankedRowDto>> ComputeRevenueByWaiterAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.WaiterId.HasValue)
                .GroupBy(o => new
                {
                    Id = o.WaiterId!.Value,
                    Name = o.Waiter != null ? (o.Waiter.FullName ?? o.Waiter.Username) : "Unknown",
                    NameAr = o.Waiter != null ? o.Waiter.FullNameAr : null
                })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.NameAr,
                    Revenue = g.Sum(o => o.TotalAmount),
                    Count   = g.Count()
                })
                .OrderByDescending(r => r.Revenue)
                .Take(10)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, Math.Round(r.Revenue, 2), null, r.Count)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> ComputeRevenueByTableAsync(CancellationToken ct)
        {
            var orders = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.TableId.HasValue)
                .Select(o => new { o.Id, o.TableId, o.TableName, o.TotalAmount })
                .ToListAsync(ct);

            var orderIds = orders.Select(o => o.Id).ToList();
            var costByOrder = await _q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .Where(oi => orderIds.Contains(oi.OrderId))
                .GroupBy(oi => oi.OrderId)
                .Select(g => new { OrderId = g.Key, Cost = g.Sum(oi => oi.StockDeductedCost) })
                .ToDictionaryAsync(r => r.OrderId, r => r.Cost, ct);

            return orders
                .GroupBy(o => new { o.TableId, o.TableName })
                .Select(r => new RankedRowDto(
                    r.Key.TableId,
                    string.IsNullOrWhiteSpace(r.Key.TableName) ? "—" : r.Key.TableName,
                    null,
                    Math.Round(r.Sum(o => o.TotalAmount), 2),
                    Math.Round(r.Sum(o => o.TotalAmount - (costByOrder.TryGetValue(o.Id, out var cost) ? cost : 0m)), 2),
                    r.Count()))
                .OrderByDescending(r => r.PrimaryValue)
                .Take(10)
                .ToList();
        }

        private async Task<IReadOnlyList<TimeSeriesPointDto>> ComputeRevenueByHourAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .GroupBy(o => (o.PaidAt ?? o.CreatedAt).Hour)
                .Select(g => new
                {
                    Hour    = g.Key,
                    Revenue = g.Sum(o => o.TotalAmount),
                    Count   = g.Count()
                })
                .ToListAsync(ct);

            // Hour-of-day encoded on a fixed epoch day; the frontend reads the UTC
            // hour off the bucket. Window timezone handling matches the existing
            // operations hourly series (UTC-bucketed).
            return rows
                .OrderBy(r => r.Hour)
                .Select(r => new TimeSeriesPointDto(
                    new DateTime(2000, 1, 1, r.Hour, 0, 0, DateTimeKind.Utc),
                    Math.Round(r.Revenue, 2),
                    r.Count))
                .ToList();
        }

        private async Task<(IReadOnlyList<GrowthRowDto> Products, IReadOnlyList<GrowthRowDto> Categories)> ComputeGrowthAsync(CancellationToken ct)
        {
            var prevWindow = _ctx.Filter.Compare == ComparisonMode.PreviousYear
                ? _ctx.Window.PreviousYear()
                : _ctx.Window.PreviousPeriod();

            var prevFilter = _ctx.Filter.Clone();
            prevFilter.StartUtc = prevWindow.StartUtc;
            prevFilter.EndUtc   = prevWindow.EndUtc;
            prevFilter.Preset   = DashboardFilterPreset.Custom;
            prevFilter.Compare  = ComparisonMode.None;

            var prevCtx = new DashboardFilterContext();
            prevCtx.Initialise(prevFilter, prevWindow, _ctx.Scope);
            var prevQ = _q.ForContext(prevCtx);

            var curProducts  = await ProductRevenueRowsAsync(_q, ct);
            var prevProducts = await ProductRevenueRowsAsync(prevQ, ct);
            var curCats      = await CategoryRevenueRowsAsync(_q, ct);
            var prevCats     = await CategoryRevenueRowsAsync(prevQ, ct);

            var prevProductMap = prevProducts.ToDictionary(p => p.Id, p => p.Revenue);
            var products = curProducts
                .Select(c =>
                {
                    prevProductMap.TryGetValue(c.Id, out var prev);
                    return new GrowthRowDto
                    {
                        Id = c.Id, Name = c.Name, NameAr = c.NameAr,
                        Current = Math.Round(c.Revenue, 2), Previous = Math.Round(prev, 2),
                        ChangePercent = GrowthPct(c.Revenue, prev)
                    };
                })
                .OrderByDescending(r => r.Current)
                .Take(15)
                .ToList();

            var catIds = curCats.Select(c => c.Id).Concat(prevCats.Select(c => c.Id))
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var catNames = await _db.Categories.AsNoTracking()
                .Where(c => catIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.NameAr })
                .ToDictionaryAsync(c => c.Id, ct);

            var prevCatMap = prevCats.ToDictionary(c => c.Id ?? Guid.Empty, c => c.Revenue);
            var categories = curCats
                .Select(c =>
                {
                    prevCatMap.TryGetValue(c.Id ?? Guid.Empty, out var prev);
                    catNames.TryGetValue(c.Id ?? Guid.Empty, out var meta);
                    return new GrowthRowDto
                    {
                        Id = c.Id,
                        Name = meta?.Name ?? "Uncategorized",
                        NameAr = meta?.NameAr,
                        Current = Math.Round(c.Revenue, 2), Previous = Math.Round(prev, 2),
                        ChangePercent = GrowthPct(c.Revenue, prev)
                    };
                })
                .OrderByDescending(r => r.Current)
                .ToList();

            return (products, categories);

            static decimal GrowthPct(decimal cur, decimal prev)
                => prev == 0 ? (cur > 0 ? 100m : 0m) : Math.Round((cur - prev) / prev * 100m, 2);
        }

        private static async Task<IReadOnlyList<(Guid Id, string Name, string? NameAr, decimal Revenue)>> ProductRevenueRowsAsync(
            IMetricQueryBuilder q, CancellationToken ct)
        {
            var rows = await q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .GroupBy(oi => new { oi.ProductId, oi.ProductName, NameAr = oi.Product.NameAr })
                .Select(g => new { g.Key.ProductId, g.Key.ProductName, g.Key.NameAr, Revenue = g.Sum(oi => oi.Price * oi.Quantity) })
                .ToListAsync(ct);
            return rows.Select(r => (r.ProductId, r.ProductName, r.NameAr, r.Revenue)).ToList();
        }

        private static async Task<IReadOnlyList<(Guid? Id, decimal Revenue)>> CategoryRevenueRowsAsync(
            IMetricQueryBuilder q, CancellationToken ct)
        {
            var rows = await q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .GroupBy(oi => oi.Product.CategoryId)
                .Select(g => new { CategoryId = g.Key, Revenue = g.Sum(oi => oi.Price * oi.Quantity) })
                .ToListAsync(ct);
            return rows.Select(r => (r.CategoryId, r.Revenue)).ToList();
        }

        private async Task<IReadOnlyList<PurchasePriceVarianceDto>> ComputePurchasePriceVarianceAsync(CancellationToken ct)
        {
            var w = _ctx.Window;
            var query = _db.PurchaseOrders.AsNoTracking()
                .Where(po => po.Status == PurchaseOrderStatus.Received || po.Status == PurchaseOrderStatus.Approved)
                .Where(po => (po.ReceivedDate ?? po.CreatedAt) >= w.StartUtc
                          && (po.ReceivedDate ?? po.CreatedAt) < w.EndUtc);
            if (_ctx.Filter.BranchId.HasValue)
                query = query.Where(po => po.BranchId == _ctx.Filter.BranchId.Value);

            var items = await query
                .SelectMany(po => po.Items.Select(i => new
                {
                    i.RawMaterialId,
                    i.RawMaterialName,
                    NameAr = i.RawMaterial != null ? i.RawMaterial.NameAr : null,
                    i.UnitPrice,
                    Date = po.ReceivedDate ?? po.CreatedAt
                }))
                .ToListAsync(ct);

            if (items.Count == 0) return Array.Empty<PurchasePriceVarianceDto>();

            return items
                .GroupBy(i => new { i.RawMaterialId, i.RawMaterialName, i.NameAr })
                .Select(g =>
                {
                    var min = g.Min(x => x.UnitPrice);
                    var max = g.Max(x => x.UnitPrice);
                    var avg = g.Average(x => x.UnitPrice);
                    var latest = g.OrderBy(x => x.Date).Last().UnitPrice;
                    return new PurchasePriceVarianceDto
                    {
                        RawMaterialId      = g.Key.RawMaterialId,
                        Name               = g.Key.RawMaterialName ?? "—",
                        NameAr             = g.Key.NameAr,
                        MinUnitPrice       = Math.Round(min, 2),
                        MaxUnitPrice       = Math.Round(max, 2),
                        LatestUnitPrice    = Math.Round(latest, 2),
                        AverageUnitPrice   = Math.Round(avg, 2),
                        VariancePercentage = min == 0 ? 0 : Math.Round((max - min) / min * 100m, 2),
                        PurchaseCount      = g.Count()
                    };
                })
                .Where(v => v.PurchaseCount > 1)
                .OrderByDescending(v => v.VariancePercentage)
                .Take(15)
                .ToList();
        }

        private async Task<IReadOnlyList<CategorySliceDto>> ComputeRevenueByWeekdayAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .GroupBy(o => (o.PaidAt ?? o.CreatedAt).DayOfWeek)
                .Select(g => new
                {
                    Day     = g.Key,
                    Revenue = g.Sum(o => o.TotalAmount),
                    Count   = g.Count()
                })
                .ToListAsync(ct);

            if (rows.Count == 0) return Array.Empty<CategorySliceDto>();

            var total = rows.Sum(r => r.Revenue);
            // Always emit all 7 days in week order so the chart is stable.
            var byDay = rows.ToDictionary(r => r.Day, r => (r.Revenue, r.Count));
            var order = new[]
            {
                DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
                DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
            };
            return order.Select(day =>
            {
                byDay.TryGetValue(day, out var v);
                return new CategorySliceDto(
                    WeekdayName(day, false),
                    WeekdayName(day, true),
                    Math.Round(v.Revenue, 2),
                    total == 0 ? 0 : Math.Round(v.Revenue / total * 100m, 2),
                    v.Count);
            }).ToList();
        }

        private static string WeekdayName(DayOfWeek day, bool arabic) => day switch
        {
            DayOfWeek.Saturday  => arabic ? "السبت"    : "Saturday",
            DayOfWeek.Sunday    => arabic ? "الأحد"    : "Sunday",
            DayOfWeek.Monday    => arabic ? "الإثنين"  : "Monday",
            DayOfWeek.Tuesday   => arabic ? "الثلاثاء" : "Tuesday",
            DayOfWeek.Wednesday => arabic ? "الأربعاء" : "Wednesday",
            DayOfWeek.Thursday  => arabic ? "الخميس"   : "Thursday",
            _                   => arabic ? "الجمعة"   : "Friday"
        };

        private async Task<IReadOnlyList<RefundReasonDto>> ComputeRefundReasonsAsync(CancellationToken ct)
        {
            var rows = await _q.RefundLogs()
                .GroupBy(r => r.RefundReason)
                .Select(g => new
                {
                    Reason = g.Key,
                    Count  = g.Count(),
                    Amount = g.Sum(r => r.RefundAmount)
                })
                .ToListAsync(ct);

            if (rows.Count == 0) return Array.Empty<RefundReasonDto>();

            var total = rows.Sum(r => r.Amount);
            return rows
                .Select(r => new RefundReasonDto
                {
                    Reason     = string.IsNullOrWhiteSpace(r.Reason) ? "Unspecified" : r.Reason,
                    Count      = r.Count,
                    Amount     = Math.Round(r.Amount, 2),
                    Percentage = total == 0 ? 0 : Math.Round(r.Amount / total * 100m, 2)
                })
                .OrderByDescending(r => r.Amount)
                .ToList();
        }

        private async Task<IReadOnlyList<TaxBreakdownDto>> ComputeTaxBreakdownAsync(CancellationToken ct)
        {
            var rows = await _q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.TaxAmount > 0)
                .GroupBy(o => o.TaxRate)
                .Select(g => new
                {
                    Rate         = g.Key,
                    TaxableSales = g.Sum(o => o.Subtotal),
                    TaxAmount    = g.Sum(o => o.TaxAmount),
                    OrderCount   = g.Count()
                })
                .ToListAsync(ct);

            return rows
                .Select(r => new TaxBreakdownDto
                {
                    Rate         = r.Rate,
                    TaxableSales = Math.Round(r.TaxableSales, 2),
                    TaxAmount    = Math.Round(r.TaxAmount, 2),
                    OrderCount   = r.OrderCount
                })
                .OrderByDescending(r => r.TaxAmount)
                .ToList();
        }

        private IQueryable<CancelLog> CancelledBeforePaymentLogs()
            => _q.CancelLogs()
                .Where(c => c.Order.Status == OrderStatus.Cancelled)
                .Where(c => !_db.RefundLogs.Any(r => r.OrderId == c.OrderId))
                .Where(c => !c.Order.PaidAt.HasValue
                    && !c.Order.Payments.Any()
                    && (c.Order.PaymentMethod == null
                        || c.Order.PaymentMethod == string.Empty));

        private static async Task<IReadOnlyList<RankedRowDto>> ComputeRankedProductsAsync(IMetricQueryBuilder q, bool descending, CancellationToken ct)
        {
            var query = q.OrderItems()
                .Where(OrderLifecyclePredicates.RecordedSaleItem)
                .GroupBy(oi => new { oi.ProductId, oi.ProductName, ProductNameAr = oi.Product.NameAr })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.ProductNameAr,
                    Revenue  = g.Sum(oi => oi.Price * oi.Quantity),
                    Quantity = g.Sum(oi => oi.Quantity),
                });

            var ordered = descending
                ? query.OrderByDescending(r => r.Revenue)
                : query.OrderBy(r => r.Revenue);

            var rows = await ordered.Take(10).ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.ProductId, r.ProductName, r.ProductNameAr, r.Revenue, null, r.Quantity)).ToList();
        }

        private async Task<IReadOnlyList<RankedRowDto>> ComputeRevenueByCashierAsync(IMetricQueryBuilder q, CancellationToken ct)
        {
            var rows = await q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.PaidByUserId.HasValue)
                .GroupBy(o => new
                {
                    Id = o.PaidByUserId!.Value,
                    Name = o.PaidByUser != null ? (o.PaidByUser.FullName ?? o.PaidByUser.Username) : "Unknown",
                    NameAr = o.PaidByUser != null ? o.PaidByUser.FullNameAr : null
                })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.NameAr,
                    Revenue = g.Sum(o => o.TotalAmount),
                    Count   = g.Count(),
                })
                .OrderByDescending(r => r.Revenue)
                .Take(10)
                .ToListAsync(ct);
            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, r.Revenue, null, r.Count)).ToList();
        }

        private static async Task<IReadOnlyList<RankedRowDto>> ComputeDiscountsByUserAsync(
            IMetricQueryBuilder q,
            CancellationToken ct)
        {
            var rows = await q.Orders()
                .Where(OrderLifecyclePredicates.RecordedSale)
                .Where(o => o.DiscountAmount > 0 && o.PaidByUserId.HasValue)
                .GroupBy(o => new
                {
                    Id = o.PaidByUserId!.Value,
                    Name = o.PaidByUser != null ? (o.PaidByUser.FullName ?? o.PaidByUser.Username) : "Unknown",
                    NameAr = o.PaidByUser != null ? o.PaidByUser.FullNameAr : null
                })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.NameAr,
                    Total = g.Sum(o => o.DiscountAmount),
                    Count = g.Count()
                })
                .OrderByDescending(r => r.Total)
                .Take(10)
                .ToListAsync(ct);

            return rows.Select(r => new RankedRowDto(r.Id, r.Name, r.NameAr, r.Total, null, r.Count)).ToList();
        }

        private async Task<PeriodComparisonDto> ComputeComparisonAsync(CancellationToken ct)
        {
            // Build a sibling context bound to the previous window and reuse the
            // calculator helpers above.
            var prevWindow = _ctx.Filter.Compare == ComparisonMode.PreviousYear
                ? _ctx.Window.PreviousYear()
                : _ctx.Window.PreviousPeriod();

            var prevFilter = _ctx.Filter.Clone();
            prevFilter.StartUtc = prevWindow.StartUtc;
            prevFilter.EndUtc   = prevWindow.EndUtc;
            prevFilter.Preset   = DashboardFilterPreset.Custom;
            prevFilter.Compare  = ComparisonMode.None;

            var prevCtx = new DashboardFilterContext();
            prevCtx.Initialise(prevFilter, prevWindow, _ctx.Scope);
            var prevQ = _q.ForContext(prevCtx);

            var prevRev    = await ComputeRevenueAsync(prevQ, prevWindow, ct);
            var prevProfit = await ComputeProfitabilityAsync(prevQ, prevWindow, prevRev, ct);

            var current = await ComputeRevenueAsync(_q, _ctx.Window, ct);
            var currentProfit = await ComputeProfitabilityAsync(_q, _ctx.Window, current, ct);

            static decimal Pct(decimal cur, decimal prev) =>
                prev == 0 ? (cur > 0 ? 100m : 0m) : Math.Round((cur - prev) / prev * 100m, 2);

            return new PeriodComparisonDto
            {
                PreviousRevenue        = prevRev,
                PreviousProfitability  = prevProfit,
                RevenueChangePercent   = Pct(current.Net, prevRev.Net),
                NetProfitChangePercent = Pct(currentProfit.NetProfit, prevProfit.NetProfit),
                MarginChangePercent    = Pct(currentProfit.MarginPercent, prevProfit.MarginPercent)
            };
        }

        private sealed record PaymentCommissionOrderRow(
            Guid OrderId,
            string OrderNumber,
            DateTime DateUtc,
            string Customer,
            decimal OrderTotal,
            decimal GrossAmount,
            Guid? PaymentMethodId,
            string? PaymentMethodCode,
            string? PaymentMethodName,
            string? PaymentMethodNameAr,
            string? Method,
            decimal CommissionAmount,
            decimal RestaurantShare,
            decimal ProviderShare,
            string Basis = CommissionBasis.Snapshot);

        private sealed record PartnerCommissionOrderRow(
            Guid OrderId,
            string OrderNumber,
            DateTime DateUtc,
            string Customer,
            decimal OrderTotal,
            OrderSource OrderSource,
            Guid? DeliveryPartnerId,
            string? DeliveryPartnerName,
            string? DeliveryPartnerNameAr,
            string? DeliveryPartnerCode,
            decimal CommissionAmount,
            decimal RestaurantShare,
            decimal ProviderShare,
            string Basis = CommissionBasis.Snapshot);

        // ─── Historical reconstruction support types ───────────────────────────
        private sealed record ProviderConfig(
            Guid Id,
            string? Code,
            string Name,
            string? NameAr,
            CostSharingMode Mode,
            decimal CommissionPct,
            decimal RestaurantPct,
            decimal CounterpartyPct);

        private sealed record AuditPoint(DateTime AtUtc, string? PreviousJson, string? NewJson);

        private sealed record EffectiveRule(
            CostSharingMode Mode,
            decimal CommissionPct,
            decimal RestaurantPct,
            decimal CounterpartyPct);

        private sealed record AuditedRuleJson
        {
            public int Mode { get; init; }
            public decimal RestaurantPercentage { get; init; }
            public decimal CounterpartyPercentage { get; init; }
            public decimal CostSharingCommissionPercentage { get; init; }
        }

        private sealed record HistoricalCommissionResult(
            IReadOnlyList<PartnerCommissionOrderRow> PartnerRows,
            IReadOnlyList<PaymentCommissionOrderRow> PaymentRows,
            int ExcludedCount)
        {
            public static HistoricalCommissionResult Empty { get; } = new(
                Array.Empty<PartnerCommissionOrderRow>(),
                Array.Empty<PaymentCommissionOrderRow>(),
                0);
        }

        private sealed class CommissionConfigDirectory
        {
            public required IReadOnlyDictionary<Guid, ProviderConfig> PartnersById { get; init; }
            public required IReadOnlyDictionary<string, ProviderConfig> PartnersByCode { get; init; }
            public required IReadOnlyDictionary<string, ProviderConfig> PartnersByName { get; init; }
            public required IReadOnlyDictionary<Guid, ProviderConfig> MethodsById { get; init; }
            public required IReadOnlyDictionary<string, ProviderConfig> MethodsByCode { get; init; }
            public required IReadOnlyDictionary<string, ProviderConfig> MethodsByName { get; init; }
            public required IReadOnlyDictionary<Guid, List<AuditPoint>> Timelines { get; init; }
        }

        private sealed record AdvancedFinancialAnalytics(
            IReadOnlyList<PaymentMethodAnalyticsDto> PaymentMethods,
            IReadOnlyList<OrderSourceAnalyticsDto> OrderSources,
            IReadOnlyList<PartnerAnalyticsDto> Partners,
            IReadOnlyList<CategorySliceDto> CardBreakdown,
            IReadOnlyList<CategorySliceDto> PayMobBreakdown)
        {
            public static AdvancedFinancialAnalytics Empty { get; } = new(
                Array.Empty<PaymentMethodAnalyticsDto>(),
                Array.Empty<OrderSourceAnalyticsDto>(),
                Array.Empty<PartnerAnalyticsDto>(),
                Array.Empty<CategorySliceDto>(),
                Array.Empty<CategorySliceDto>());
        }

        private sealed record PaymentAggregateRow(
            Guid? PaymentMethodId,
            string? PaymentMethodCode,
            string? PaymentMethodName,
            string? PaymentMethodNameAr,
            string? Method,
            int OrderCount,
            decimal CollectedRevenue,
            decimal GrossRevenue,
            decimal DiscountAmount,
            decimal TotalCommission,
            decimal RestaurantShare,
            decimal CounterpartyShare);

        private sealed record LegacyPaymentAggregateRow(
            string? Method,
            int OrderCount,
            decimal CollectedRevenue,
            decimal GrossRevenue,
            decimal DiscountAmount);

        private sealed record OrderSourceAggregateRow(
            OrderSource OrderSource,
            OrderType OrderType,
            int OrderCount,
            decimal GrossRevenue,
            decimal NetRevenue);

        private sealed record PartnerAggregateRow(
            OrderSource OrderSource,
            Guid? DeliveryPartnerId,
            string? DeliveryPartnerName,
            string? DeliveryPartnerNameAr,
            string? DeliveryPartnerCode,
            int OrderCount,
            decimal GrossRevenue,
            decimal NetRevenue,
            decimal DeliveryFees,
            decimal PartnerFees,
            decimal TotalCommission,
            decimal RestaurantShare,
            decimal CounterpartyShare,
            decimal NetSettlement,
            decimal NetRevenueAfterCostSharing);

        private sealed record PartnerSettlementAggregateRow(
            OrderSource OrderSource,
            Guid? DeliveryPartnerId,
            string? DeliveryPartnerName,
            string? DeliveryPartnerCode,
            string? SettlementMethod,
            int OrderCount,
            decimal Revenue);

        private sealed record RefundAggregateRow(
            string? PaymentMethod,
            OrderSource OrderSource,
            OrderType OrderType,
            Guid? DeliveryPartnerId,
            string? DeliveryPartnerName,
            string? DeliveryPartnerCode,
            int OrderCount,
            decimal Amount);

        private sealed record CancellationAggregateRow(
            OrderSource OrderSource,
            OrderType OrderType,
            int OrderCount,
            decimal Amount);

        private sealed record PaymentMethodMetadata(
            Guid Id,
            string Code,
            string Name,
            string? NameAr,
            bool IsActive);

        private sealed record PartnerMetadata(
            Guid Id,
            string Code,
            string Name,
            string? NameAr,
            bool IsActive);

        private sealed record PaymentDescriptor(
            string Key,
            string Name,
            string? NameAr,
            bool IsActive);

        private sealed record OrderSourceDescriptor(
            string Key,
            string Name,
            string NameAr);

        private sealed record PartnerDescriptor(
            string Key,
            string Name,
            string? NameAr,
            bool IsActive);

        private sealed record BreakdownAggregate(
            string Key,
            string Name,
            string NameAr,
            decimal Value,
            int Count);

        private sealed class PaymentAnalyticsAccumulator
        {
            private readonly HashSet<string> _aliases = new(StringComparer.OrdinalIgnoreCase);

            public PaymentAnalyticsAccumulator(PaymentDescriptor descriptor)
            {
                Key = descriptor.Key;
                Name = descriptor.Name;
                NameAr = descriptor.NameAr;
                IsActive = descriptor.IsActive;
                _aliases.Add(descriptor.Key);
                _aliases.Add(descriptor.Name);
            }

            public string Key { get; }
            public string Name { get; }
            public string? NameAr { get; }
            public bool IsActive { get; }
            public int OrderCount { get; private set; }
            public decimal CollectedRevenue { get; private set; }
            public decimal GrossRevenue { get; private set; }
            public decimal DiscountAmount { get; private set; }
            public decimal TotalCommission { get; private set; }
            public decimal RestaurantShare { get; private set; }
            public decimal CounterpartyShare { get; private set; }
            public int RefundedOrderCount { get; set; }
            public decimal RefundAmount { get; set; }

            public void Add(
                int orderCount,
                decimal collectedRevenue,
                decimal grossRevenue,
                decimal discountAmount,
                decimal totalCommission,
                decimal restaurantShare,
                decimal counterpartyShare)
            {
                OrderCount += orderCount;
                CollectedRevenue += collectedRevenue;
                GrossRevenue += grossRevenue;
                DiscountAmount += discountAmount;
                TotalCommission += totalCommission;
                RestaurantShare += restaurantShare;
                CounterpartyShare += counterpartyShare;
            }

            public bool Matches(string? value)
                => !string.IsNullOrWhiteSpace(value) && _aliases.Contains(value.Trim());
        }

        private sealed class OrderSourceAccumulator
        {
            public OrderSourceAccumulator(OrderSourceDescriptor descriptor)
            {
                Key = descriptor.Key;
                Name = descriptor.Name;
                NameAr = descriptor.NameAr;
            }

            public string Key { get; }
            public string Name { get; }
            public string NameAr { get; }
            public int OrderCount { get; private set; }
            public decimal GrossRevenue { get; private set; }
            public decimal NetRevenue { get; private set; }
            public int RefundedOrderCount { get; set; }
            public decimal RefundAmount { get; set; }
            public int CancelledBeforePaymentCount { get; set; }
            public decimal CancelledBeforePaymentAmount { get; set; }

            public void Add(int count, decimal grossRevenue, decimal netRevenue)
            {
                OrderCount += count;
                GrossRevenue += grossRevenue;
                NetRevenue += netRevenue;
            }
        }

        private sealed class PartnerAnalyticsAccumulator
        {
            public PartnerAnalyticsAccumulator(PartnerDescriptor descriptor)
            {
                Key = descriptor.Key;
                Name = descriptor.Name;
                NameAr = descriptor.NameAr;
                IsActive = descriptor.IsActive;
            }

            public string Key { get; }
            public string Name { get; }
            public string? NameAr { get; }
            public bool IsActive { get; }
            public int OrderCount { get; private set; }
            public decimal GrossRevenue { get; private set; }
            public decimal NetRevenue { get; private set; }
            public decimal DeliveryFees { get; private set; }
            public decimal PartnerFees { get; private set; }
            public decimal RefundAmount { get; set; }
            public decimal TotalCommission { get; private set; }
            public decimal RestaurantShare { get; private set; }
            public decimal CounterpartyShare { get; private set; }
            public decimal NetSettlement { get; private set; }
            public decimal NetRevenueAfterCostSharing { get; private set; }
            public int CashOrderCount { get; private set; }
            public decimal CashRevenue { get; private set; }
            public int CreditOrderCount { get; private set; }
            public decimal CreditRevenue { get; private set; }

            public void Add(PartnerAggregateRow row)
            {
                OrderCount += row.OrderCount;
                GrossRevenue += row.GrossRevenue;
                NetRevenue += row.NetRevenue;
                DeliveryFees += row.DeliveryFees;
                PartnerFees += row.PartnerFees;
                TotalCommission += row.TotalCommission;
                RestaurantShare += row.RestaurantShare;
                CounterpartyShare += row.CounterpartyShare;
                NetSettlement += row.NetSettlement;
                NetRevenueAfterCostSharing += row.NetRevenueAfterCostSharing;
            }

            public void AddSettlement(PartnerSettlementAggregateRow row)
            {
                if (IsCashSettlement(row.SettlementMethod))
                {
                    CashOrderCount += row.OrderCount;
                    CashRevenue += row.Revenue;
                    return;
                }
                CreditOrderCount += row.OrderCount;
                CreditRevenue += row.Revenue;
            }

            public PartnerAnalyticsDto ToDto()
                => new()
                {
                    Key = Key,
                    Name = Name,
                    NameAr = NameAr,
                    IsActive = IsActive,
                    OrderCount = OrderCount,
                    GrossRevenue = Math.Round(GrossRevenue, 2),
                    NetRevenue = Math.Round(NetRevenue - RefundAmount, 2),
                    DeliveryFees = Math.Round(DeliveryFees, 2),
                    PartnerFees = Math.Round(PartnerFees, 2),
                    RefundAmount = Math.Round(RefundAmount, 2),
                    TotalCommission = Math.Round(TotalCommission, 2),
                    RestaurantShare = Math.Round(RestaurantShare, 2),
                    CounterpartyShare = Math.Round(CounterpartyShare, 2),
                    NetSettlement = Math.Round(NetSettlement - RefundAmount, 2),
                    NetRevenueAfterCostSharing = Math.Round(
                        NetRevenueAfterCostSharing - RefundAmount, 2),
                    AverageOrderValue = OrderCount == 0
                        ? 0
                        : Math.Round(GrossRevenue / OrderCount, 2),
                    CashOrderCount = CashOrderCount,
                    CashRevenue = Math.Round(CashRevenue, 2),
                    CreditOrderCount = CreditOrderCount,
                    CreditRevenue = Math.Round(CreditRevenue, 2)
                };
        }
    }
}
