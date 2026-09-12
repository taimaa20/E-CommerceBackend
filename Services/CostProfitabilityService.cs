using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Repositories;

namespace RestaurantPos.Api.Services
{
    public class CostProfitabilityService : ICostProfitabilityService
    {
        private readonly ICostProfitabilityRepository _repository;
        private readonly ITenantResolver _tenantResolver;
        private readonly IConfigAuditService _audit;
        private readonly ILogger<CostProfitabilityService> _logger;

        public CostProfitabilityService(
            ICostProfitabilityRepository repository,
            ITenantResolver tenantResolver,
            IConfigAuditService audit,
            ILogger<CostProfitabilityService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tenantResolver = tenantResolver ?? throw new ArgumentNullException(nameof(tenantResolver));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<CostSharingProviderDto>> GetProvidersAsync(
            CostProviderType? type,
            bool includeInactive,
            CancellationToken ct)
            => (await _repository.GetProvidersAsync(type, includeInactive, ct))
                .Select(MapProvider)
                .ToList();

        public async Task<CostSharingProviderDto> GetProviderByIdAsync(Guid id, CancellationToken ct)
            => MapProvider(await GetProviderAsync(id, ct));

        public async Task<CostSharingProviderDto> CreateProviderAsync(
            CostSharingProviderUpsertDto dto,
            CancellationToken ct)
        {
            ValidateProvider(dto);
            var code = NormalizeCode(dto.Code);
            if (await _repository.ProviderCodeExistsAsync(code, null, ct))
                throw new ConflictException($"A cost provider with code '{code}' already exists.");

            var provider = BuildProvider(dto, code);
            var created = await _repository.AddProviderAsync(provider, ct);
            await AuditConfigAsync(null, ProviderSnapshot(created), created.Id, "CostSharingProvider", ct);
            return MapProvider(created);
        }

        public async Task<CostSharingProviderDto> UpdateProviderAsync(
            Guid id,
            CostSharingProviderUpsertDto dto,
            CancellationToken ct)
        {
            ValidateProvider(dto);
            var provider = await GetProviderAsync(id, ct);
            var previous = ProviderSnapshot(provider);
            var code = NormalizeCode(dto.Code);

            if (await _repository.ProviderCodeExistsAsync(code, id, ct))
                throw new ConflictException($"A cost provider with code '{code}' already exists.");

            ApplyProvider(provider, dto, code);
            var updated = await _repository.UpdateProviderAsync(provider, ct);
            await AuditConfigAsync(previous, ProviderSnapshot(updated), updated.Id, "CostSharingProvider", ct);
            return MapProvider(updated);
        }

        public async Task DeleteProviderAsync(Guid id, CancellationToken ct)
        {
            var provider = await GetProviderAsync(id, ct);
            await _repository.DeleteProviderAsync(provider, ct);
            await AuditConfigAsync(ProviderSnapshot(provider), null, provider.Id, "CostSharingProviderDeleted", ct);
        }

        public async Task<List<CostSharingRuleDto>> GetRulesAsync(
            CostProviderType? providerType,
            bool includeInactive,
            CancellationToken ct)
            => (await _repository.GetRulesAsync(providerType, includeInactive, ct))
                .Select(MapRule)
                .ToList();

        public async Task<CostSharingRuleDto> GetRuleByIdAsync(Guid id, CancellationToken ct)
            => MapRule(await GetRuleAsync(id, ct));

        public async Task<CostSharingRuleDto> CreateRuleAsync(
            CostSharingRuleUpsertDto dto,
            CancellationToken ct)
        {
            ValidateRule(dto);
            var rule = BuildRule(dto);
            var created = await _repository.AddRuleAsync(rule, ct);
            await AuditConfigAsync(null, RuleSnapshot(created), created.Id, "CostSharingRule", ct);
            return MapRule(created);
        }

        public async Task<CostSharingRuleDto> UpdateRuleAsync(
            Guid id,
            CostSharingRuleUpsertDto dto,
            CancellationToken ct)
        {
            ValidateRule(dto);
            var rule = await GetRuleAsync(id, ct);
            var previous = RuleSnapshot(rule);
            ApplyRule(rule, dto);
            var updated = await _repository.UpdateRuleAsync(rule, ct);
            await AuditConfigAsync(previous, RuleSnapshot(updated), updated.Id, "CostSharingRule", ct);
            return MapRule(updated);
        }

        public async Task DeleteRuleAsync(Guid id, CancellationToken ct)
        {
            var rule = await GetRuleAsync(id, ct);
            await _repository.DeleteRuleAsync(rule, ct);
            await AuditConfigAsync(RuleSnapshot(rule), null, rule.Id, "CostSharingRuleDeleted", ct);
        }

        public async Task<OrderProfitabilitySnapshotDto?> CaptureSnapshotAsync(
            Guid orderId,
            ProfitabilitySnapshotSource source,
            CancellationToken ct)
        {
            var order = await _repository.GetOrderForSnapshotAsync(orderId, ct);
            if (order == null) return null;
            if (!OrderPaymentHelper.BuildSnapshot(order).IsPaid) return null;

            var snapshot = await _repository.GetSnapshotForUpdateAsync(orderId, ct);
            if (snapshot?.IsFinalized == true) return MapSnapshot(snapshot);

            snapshot = UpsertSnapshot(order, snapshot, source);
            await _repository.SaveChangesAsync(ct);
            _logger.LogInformation("Profitability snapshot captured for order {OrderId}", orderId);
            return MapSnapshot(snapshot);
        }

        public async Task<OrderProfitabilitySnapshotDto> GetOrderSnapshotAsync(Guid orderId, CancellationToken ct)
        {
            var snapshot = await _repository.GetSnapshotByOrderIdAsync(orderId, ct)
                ?? throw new NotFoundException($"Profitability snapshot for order {orderId} was not found.");
            return MapSnapshot(snapshot);
        }

        public async Task<CostProfitabilityDashboardDto> GetDashboardAsync(
            CostProfitabilityFilterDto filter,
            CancellationToken ct)
        {
            var normalized = NormalizeFilter(filter);
            return new CostProfitabilityDashboardDto
            {
                Summary = await _repository.GetSummaryAsync(normalized, ct),
                Partners = await _repository.GetPartnerAnalyticsAsync(normalized, ct),
                PaymentMethods = await _repository.GetPaymentMethodAnalyticsAsync(normalized, ct),
                Products = await _repository.GetProductProfitabilityAsync(normalized, ct),
                Customers = await _repository.GetCustomerProfitabilityAsync(normalized, ct)
            };
        }

        private OrderProfitabilitySnapshot UpsertSnapshot(
            Order order,
            OrderProfitabilitySnapshot? snapshot,
            ProfitabilitySnapshotSource source)
        {
            snapshot ??= NewSnapshot(order);
            ApplySnapshotHeader(snapshot, order, source);
            var values = CalculateSnapshotValues(order);
            ApplySnapshotValues(snapshot, values);
            ReplaceItems(snapshot, BuildItemSnapshots(order, snapshot.Id, values));
            return snapshot;
        }

        private OrderProfitabilitySnapshot NewSnapshot(Order order)
        {
            var snapshot = new OrderProfitabilitySnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                OrderId = order.Id
            };

            _repository.AddSnapshot(snapshot);
            return snapshot;
        }

        private static void ApplySnapshotHeader(
            OrderProfitabilitySnapshot snapshot,
            Order order,
            ProfitabilitySnapshotSource source)
        {
            var payment = order.Payments
                .OrderByDescending(p => p.Amount)
                .ThenBy(p => p.CreatedAt)
                .FirstOrDefault();

            snapshot.OrderNumber = ResolveOrderNumber(order);
            snapshot.OrderType = order.OrderType;
            snapshot.OrderSource = order.OrderSource;
            snapshot.OrderStatus = order.Status;
            snapshot.PaidAt = order.PaidAt;
            snapshot.CashierId = order.PaidByUserId;
            snapshot.CashierName = order.PaidByUser?.FullName ?? order.PaidByUser?.Username;
            snapshot.CustomerId = order.CustomerId;
            snapshot.CustomerName = order.Customer?.Name ?? order.PartnerCustomerName ?? order.TalabatCustomerName;
            snapshot.DeliveryPartnerId = order.DeliveryPartnerId;
            snapshot.DeliveryPartnerName = ResolvePartnerName(order);
            snapshot.DeliveryPartnerNameAr = order.DeliveryPartnerNameAr;
            snapshot.DeliveryPartnerCode = order.DeliveryPartnerCode;
            snapshot.PaymentMethodId = payment?.PaymentMethodId;
            snapshot.PaymentMethodName = payment?.PaymentMethodName ?? order.PaymentMethod;
            snapshot.PaymentMethodNameAr = payment?.PaymentMethodNameAr;
            snapshot.PaymentMethodCode = payment?.PaymentMethodCode;
            snapshot.Source = source;
            snapshot.SnapshotAt = DateTime.UtcNow;
            snapshot.IsFinalized = OrderCompletion.IsCompleted(order);
            snapshot.FinalizedAt = snapshot.IsFinalized ? snapshot.FinalizedAt ?? DateTime.UtcNow : null;
        }

        private static SnapshotValues CalculateSnapshotValues(Order order)
        {
            var paymentCommission = order.Payments.Sum(p => p.CostSharingCommissionAmount);
            var paymentRestaurantShare = order.Payments.Sum(p => p.CostSharingRestaurantShareAmount);
            var partnerCommission = Math.Max(0m, order.CostSharingTotalCommission - paymentCommission);
            var netRevenue = order.NetRestaurantRevenue > 0m ? order.NetRestaurantRevenue : order.TotalAmount;
            var foodCost = order.TotalCost > 0m ? order.TotalCost : order.OrderItems.Sum(i => i.StockDeductedCost);
            var deliveryCost = ResolveDeliveryCost(order);
            var deliveryFees = order.MarketplaceDeliveryFee + order.MarketplaceServiceFee;
            var restaurantFees = order.CostSharingRestaurantShare + deliveryCost;
            var grossProfit = netRevenue - foodCost;
            var operatingProfit = grossProfit - deliveryCost - order.CostSharingRestaurantShare;
            var contribution = netRevenue - foodCost - deliveryCost - order.CostSharingRestaurantShare;

            return new SnapshotValues(
                GrossSales: Round(order.Subtotal > 0m ? order.Subtotal : order.TotalAmount),
                Discounts: Round(order.DiscountAmount + order.VoucherDiscountAmount),
                ServiceCharges: Round(order.ServiceChargeAmount),
                Taxes: Round(order.TaxAmount),
                NetSales: Round(netRevenue),
                ProviderCommission: Round(order.CostSharingTotalCommission),
                RestaurantShare: Round(order.CostSharingRestaurantShare),
                ProviderShare: Round(order.CostSharingCounterpartyShare),
                CardFees: Round(paymentCommission),
                GatewayFees: 0m,
                DeliveryFees: Round(deliveryFees),
                OtherOperationalFees: 0m,
                TotalExternalFees: Round(order.CostSharingTotalCommission + deliveryFees),
                TotalRestaurantFees: Round(restaurantFees),
                GrossRevenue: Round(order.TotalAmount),
                NetRevenue: Round(netRevenue),
                FoodCost: Round(foodCost),
                PackagingCost: 0m,
                DeliveryCost: Round(deliveryCost),
                PartnerCommission: Round(partnerCommission),
                PaymentProcessingFee: Round(paymentRestaurantShare),
                RestaurantCostShare: Round(order.CostSharingRestaurantShare),
                ProviderCostShare: Round(order.CostSharingCounterpartyShare),
                LaborCost: 0m,
                OperationalCost: 0m,
                GrossProfit: Round(grossProfit),
                GrossMarginPercentage: Percent(grossProfit, netRevenue),
                OperatingProfit: Round(operatingProfit),
                OperatingMarginPercentage: Percent(operatingProfit, netRevenue),
                NetProfit: Round(operatingProfit),
                NetMarginPercentage: Percent(operatingProfit, netRevenue),
                ProfitPerOrder: Round(operatingProfit),
                ContributionMargin: Round(contribution),
                ContributionPercentage: Percent(contribution, netRevenue),
                CostSharingDetailsJson: order.CostSharingDetailsJson);
        }

        private static void ApplySnapshotValues(
            OrderProfitabilitySnapshot snapshot,
            SnapshotValues values)
        {
            snapshot.GrossSales = values.GrossSales;
            snapshot.Discounts = values.Discounts;
            snapshot.ServiceCharges = values.ServiceCharges;
            snapshot.Taxes = values.Taxes;
            snapshot.NetSales = values.NetSales;
            snapshot.ProviderCommission = values.ProviderCommission;
            snapshot.RestaurantShare = values.RestaurantShare;
            snapshot.ProviderShare = values.ProviderShare;
            snapshot.CardFees = values.CardFees;
            snapshot.GatewayFees = values.GatewayFees;
            snapshot.DeliveryFees = values.DeliveryFees;
            snapshot.OtherOperationalFees = values.OtherOperationalFees;
            snapshot.TotalExternalFees = values.TotalExternalFees;
            snapshot.TotalRestaurantFees = values.TotalRestaurantFees;
            snapshot.GrossRevenue = values.GrossRevenue;
            snapshot.NetRevenue = values.NetRevenue;
            snapshot.FoodCost = values.FoodCost;
            snapshot.PackagingCost = values.PackagingCost;
            snapshot.DeliveryCost = values.DeliveryCost;
            snapshot.PartnerCommission = values.PartnerCommission;
            snapshot.PaymentProcessingFee = values.PaymentProcessingFee;
            snapshot.RestaurantCostShare = values.RestaurantCostShare;
            snapshot.ProviderCostShare = values.ProviderCostShare;
            snapshot.LaborCost = values.LaborCost;
            snapshot.OperationalCost = values.OperationalCost;
            snapshot.GrossProfit = values.GrossProfit;
            snapshot.GrossMarginPercentage = values.GrossMarginPercentage;
            snapshot.OperatingProfit = values.OperatingProfit;
            snapshot.OperatingMarginPercentage = values.OperatingMarginPercentage;
            snapshot.NetProfit = values.NetProfit;
            snapshot.NetMarginPercentage = values.NetMarginPercentage;
            snapshot.ProfitPerOrder = values.ProfitPerOrder;
            snapshot.ContributionMargin = values.ContributionMargin;
            snapshot.ContributionPercentage = values.ContributionPercentage;
            snapshot.CostSharingDetailsJson = values.CostSharingDetailsJson;
        }

        private static List<OrderProfitabilitySnapshotItem> BuildItemSnapshots(
            Order order,
            Guid snapshotId,
            SnapshotValues values)
        {
            var itemRows = order.OrderItems.Select(i => (Item: i, Revenue: ItemRevenue(i))).ToList();
            var revenueBase = itemRows.Sum(r => r.Revenue);
            return itemRows.Select(row => BuildItemSnapshot(order, snapshotId, row.Item, row.Revenue, revenueBase, values)).ToList();
        }

        private static OrderProfitabilitySnapshotItem BuildItemSnapshot(
            Order order,
            Guid snapshotId,
            OrderItem item,
            decimal revenue,
            decimal revenueBase,
            SnapshotValues values)
        {
            var ratio = revenueBase == 0m ? 0m : revenue / revenueBase;
            var commission = Round(values.RestaurantCostShare * ratio);
            var cardFee = Round(values.PaymentProcessingFee * ratio);
            var operational = Round(values.OperationalCost * ratio);
            var grossProfit = revenue - item.StockDeductedCost;
            var netProfit = grossProfit - commission - cardFee - operational;

            return new OrderProfitabilitySnapshotItem
            {
                TenantId = order.TenantId,
                SnapshotId = snapshotId,
                OrderId = order.Id,
                OrderItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProductNameAr = item.Product?.NameAr,
                Quantity = item.Quantity,
                GrossRevenue = Round(revenue),
                FoodCost = Round(item.StockDeductedCost),
                CommissionCost = commission,
                CardFeeCost = cardFee,
                OperationalCost = operational,
                GrossProfit = Round(grossProfit),
                NetProfit = Round(netProfit),
                MarginPercentage = Percent(netProfit, revenue)
            };
        }

        private static void ReplaceItems(
            OrderProfitabilitySnapshot snapshot,
            IReadOnlyCollection<OrderProfitabilitySnapshotItem> items)
        {
            snapshot.Items.Clear();
            foreach (var item in items)
            {
                snapshot.Items.Add(item);
            }
        }

        private static CostProfitabilityFilterDto NormalizeFilter(CostProfitabilityFilterDto filter)
        {
            var start = filter.DateFrom ?? DateTime.UtcNow.Date.AddDays(-30);
            var end = filter.DateTo ?? DateTime.UtcNow.Date.AddDays(1);
            if (end <= start) end = start.AddDays(1);
            filter.DateFrom = ToUtc(start);
            filter.DateTo = ToUtc(end);
            return filter;
        }

        private async Task<CostSharingProvider> GetProviderAsync(Guid id, CancellationToken ct)
            => await _repository.GetProviderByIdAsync(id, ct)
                ?? throw new NotFoundException($"Cost provider {id} was not found.");

        private async Task<CostSharingRule> GetRuleAsync(Guid id, CancellationToken ct)
            => await _repository.GetRuleByIdAsync(id, ct)
                ?? throw new NotFoundException($"Cost sharing rule {id} was not found.");

        private CostSharingProvider BuildProvider(CostSharingProviderUpsertDto dto, string code)
        {
            var provider = new CostSharingProvider
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId()
            };
            ApplyProvider(provider, dto, code);
            return provider;
        }

        private static void ApplyProvider(
            CostSharingProvider provider,
            CostSharingProviderUpsertDto dto,
            string code)
        {
            provider.Name = dto.Name.Trim();
            provider.NameAr = TrimToNull(dto.NameAr);
            provider.Code = code;
            provider.Type = dto.Type;
            provider.IsActive = dto.IsActive;
            provider.ExternalReferenceId = dto.ExternalReferenceId;
            provider.Notes = TrimToNull(dto.Notes);
        }

        private CostSharingRule BuildRule(CostSharingRuleUpsertDto dto)
        {
            var rule = new CostSharingRule
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantResolver.GetTenantId()
            };
            ApplyRule(rule, dto);
            return rule;
        }

        private static void ApplyRule(CostSharingRule rule, CostSharingRuleUpsertDto dto)
        {
            var shares = CostSharingHelper.NormalizePercentages(
                dto.Mode,
                dto.RestaurantPercentage,
                dto.CounterpartyPercentage);
            rule.Scope = dto.Scope;
            rule.ProviderType = dto.ProviderType;
            rule.ProviderId = dto.ProviderId;
            rule.DeliveryPartnerId = dto.DeliveryPartnerId;
            rule.PaymentMethodId = dto.PaymentMethodId;
            rule.BranchId = dto.BranchId;
            rule.OrderId = dto.OrderId;
            rule.OrderItemId = dto.OrderItemId;
            rule.CardTypeCode = TrimToNull(dto.CardTypeCode);
            rule.IsActive = dto.IsActive;
            rule.Priority = dto.Priority;
            rule.Mode = dto.Mode;
            rule.FeeType = dto.FeeType;
            rule.FeePercentage = CostSharingHelper.NormalizePercentage(dto.FeePercentage);
            rule.FixedFeeAmount = Round(dto.FixedFeeAmount);
            rule.MinimumFeeAmount = dto.MinimumFeeAmount.HasValue ? Round(dto.MinimumFeeAmount.Value) : null;
            rule.MaximumFeeAmount = dto.MaximumFeeAmount.HasValue ? Round(dto.MaximumFeeAmount.Value) : null;
            rule.ApplyVatOnFee = dto.ApplyVatOnFee;
            rule.VatPercentage = CostSharingHelper.NormalizePercentage(dto.VatPercentage);
            rule.RestaurantPercentage = shares.RestaurantPercentage;
            rule.CounterpartyPercentage = shares.CounterpartyPercentage;
            rule.EffectiveFromUtc = dto.EffectiveFromUtc;
            rule.EffectiveToUtc = dto.EffectiveToUtc;
            rule.TierDefinitionJson = TrimToNull(dto.TierDefinitionJson);
            rule.Reason = TrimToNull(dto.Reason);
        }

        private async Task AuditConfigAsync(
            object? previous,
            object? next,
            Guid targetId,
            string reason,
            CancellationToken ct)
        {
            await _audit.LogAsync(
                _tenantResolver.GetTenantId(),
                ConfigAuditEventType.CostSharingConfigChanged,
                previous,
                next,
                targetId,
                reason: reason,
                ct: ct);
        }

        private static void ValidateProvider(CostSharingProviderUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ValidationException("Cost provider name is required.");
            if (string.IsNullOrWhiteSpace(dto.Code))
                throw new ValidationException("Cost provider code is required.");
        }

        private static void ValidateRule(CostSharingRuleUpsertDto dto)
        {
            if (dto.Mode == CostSharingMode.Shared
                && !CostSharingHelper.SharedPercentagesTotalOneHundred(
                    dto.RestaurantPercentage,
                    dto.CounterpartyPercentage))
                throw new ValidationException("Cost sharing percentages must total 100%.");
            if (dto.MaximumFeeAmount.HasValue && dto.MinimumFeeAmount > dto.MaximumFeeAmount)
                throw new ValidationException("Minimum fee cannot exceed maximum fee.");
            if (dto.EffectiveFromUtc.HasValue
                && dto.EffectiveToUtc.HasValue
                && dto.EffectiveFromUtc.Value > dto.EffectiveToUtc.Value)
                throw new ValidationException("Rule effective end must be after effective start.");
        }

        private static CostSharingProviderDto MapProvider(CostSharingProvider provider) => new()
        {
            Id = provider.Id,
            Name = provider.Name,
            NameAr = provider.NameAr,
            Code = provider.Code,
            Type = provider.Type,
            IsActive = provider.IsActive,
            ExternalReferenceId = provider.ExternalReferenceId,
            Notes = provider.Notes,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
        };

        private static CostSharingRuleDto MapRule(CostSharingRule rule) => new()
        {
            Id = rule.Id,
            Scope = rule.Scope,
            ProviderType = rule.ProviderType,
            ProviderId = rule.ProviderId,
            ProviderName = rule.Provider?.Name,
            ProviderCode = rule.Provider?.Code,
            DeliveryPartnerId = rule.DeliveryPartnerId,
            PaymentMethodId = rule.PaymentMethodId,
            BranchId = rule.BranchId,
            OrderId = rule.OrderId,
            OrderItemId = rule.OrderItemId,
            CardTypeCode = rule.CardTypeCode,
            IsActive = rule.IsActive,
            Priority = rule.Priority,
            Mode = rule.Mode,
            FeeType = rule.FeeType,
            FeePercentage = rule.FeePercentage,
            FixedFeeAmount = rule.FixedFeeAmount,
            MinimumFeeAmount = rule.MinimumFeeAmount,
            MaximumFeeAmount = rule.MaximumFeeAmount,
            ApplyVatOnFee = rule.ApplyVatOnFee,
            VatPercentage = rule.VatPercentage,
            RestaurantPercentage = rule.RestaurantPercentage,
            CounterpartyPercentage = rule.CounterpartyPercentage,
            EffectiveFromUtc = rule.EffectiveFromUtc,
            EffectiveToUtc = rule.EffectiveToUtc,
            TierDefinitionJson = rule.TierDefinitionJson,
            Reason = rule.Reason,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };

        private static OrderProfitabilitySnapshotDto MapSnapshot(OrderProfitabilitySnapshot snapshot) => new()
        {
            Id = snapshot.Id,
            OrderId = snapshot.OrderId,
            OrderNumber = snapshot.OrderNumber,
            GrossSales = snapshot.GrossSales,
            Discounts = snapshot.Discounts,
            ServiceCharges = snapshot.ServiceCharges,
            Taxes = snapshot.Taxes,
            NetSales = snapshot.NetSales,
            ProviderCommission = snapshot.ProviderCommission,
            RestaurantShare = snapshot.RestaurantShare,
            ProviderShare = snapshot.ProviderShare,
            CardFees = snapshot.CardFees,
            GatewayFees = snapshot.GatewayFees,
            DeliveryFees = snapshot.DeliveryFees,
            OtherOperationalFees = snapshot.OtherOperationalFees,
            TotalExternalFees = snapshot.TotalExternalFees,
            TotalRestaurantFees = snapshot.TotalRestaurantFees,
            GrossRevenue = snapshot.GrossRevenue,
            NetRevenue = snapshot.NetRevenue,
            FoodCost = snapshot.FoodCost,
            PackagingCost = snapshot.PackagingCost,
            DeliveryCost = snapshot.DeliveryCost,
            PartnerCommission = snapshot.PartnerCommission,
            PaymentProcessingFee = snapshot.PaymentProcessingFee,
            RestaurantCostShare = snapshot.RestaurantCostShare,
            ProviderCostShare = snapshot.ProviderCostShare,
            LaborCost = snapshot.LaborCost,
            OperationalCost = snapshot.OperationalCost,
            GrossProfit = snapshot.GrossProfit,
            GrossMarginPercentage = snapshot.GrossMarginPercentage,
            OperatingProfit = snapshot.OperatingProfit,
            OperatingMarginPercentage = snapshot.OperatingMarginPercentage,
            NetProfit = snapshot.NetProfit,
            NetMarginPercentage = snapshot.NetMarginPercentage,
            ProfitPerOrder = snapshot.ProfitPerOrder,
            ContributionMargin = snapshot.ContributionMargin,
            ContributionPercentage = snapshot.ContributionPercentage,
            Source = snapshot.Source,
            IsFinalized = snapshot.IsFinalized,
            SnapshotAt = snapshot.SnapshotAt,
            FinalizedAt = snapshot.FinalizedAt,
            Items = snapshot.Items.Select(MapSnapshotItem).ToList()
        };

        private static OrderProfitabilitySnapshotItemDto MapSnapshotItem(
            OrderProfitabilitySnapshotItem item) => new()
        {
            OrderItemId = item.OrderItemId,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            ProductNameAr = item.ProductNameAr,
            Quantity = item.Quantity,
            GrossRevenue = item.GrossRevenue,
            FoodCost = item.FoodCost,
            CommissionCost = item.CommissionCost,
            CardFeeCost = item.CardFeeCost,
            OperationalCost = item.OperationalCost,
            GrossProfit = item.GrossProfit,
            NetProfit = item.NetProfit,
            MarginPercentage = item.MarginPercentage
        };

        private static object ProviderSnapshot(CostSharingProvider provider) => new
        {
            provider.Id,
            provider.Name,
            provider.Code,
            provider.Type,
            provider.IsActive,
            provider.ExternalReferenceId
        };

        private static object RuleSnapshot(CostSharingRule rule) => new
        {
            rule.Id,
            rule.Scope,
            rule.ProviderType,
            rule.ProviderId,
            rule.Mode,
            rule.FeeType,
            rule.FeePercentage,
            rule.FixedFeeAmount,
            rule.RestaurantPercentage,
            rule.CounterpartyPercentage,
            rule.IsActive,
            rule.Priority
        };

        private static decimal ItemRevenue(OrderItem item)
            => item.IsComplimentary
                ? 0m
                : Round((item.Price + item.Modifiers.Sum(m => m.Price * m.Quantity)) * item.Quantity);

        private static decimal ResolveDeliveryCost(Order order)
            => order.ActualDeliveryCost > 0m
                ? order.ActualDeliveryCost
                : order.DeliveryCost ?? 0m;

        private static string ResolveOrderNumber(Order order)
            => !string.IsNullOrWhiteSpace(order.DisplayOrderNumber)
                ? order.DisplayOrderNumber
                : !string.IsNullOrWhiteSpace(order.PublicOrderNumber)
                    ? order.PublicOrderNumber
                    : order.OrderNumber;

        private static string? ResolvePartnerName(Order order)
            => order.OrderSource == OrderSource.Talabat
                ? "Talabat"
                : order.DeliveryPartnerName ?? order.DeliveryPartner?.Name;

        private static DateTime ToUtc(DateTime value)
            => value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static string NormalizeCode(string value)
            => value.Trim().ToUpperInvariant().Replace(' ', '_');

        private static string? TrimToNull(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static decimal Percent(decimal value, decimal basis)
            => basis == 0m ? 0m : Round(value / basis * 100m);

        private static decimal Round(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed record SnapshotValues(
            decimal GrossSales,
            decimal Discounts,
            decimal ServiceCharges,
            decimal Taxes,
            decimal NetSales,
            decimal ProviderCommission,
            decimal RestaurantShare,
            decimal ProviderShare,
            decimal CardFees,
            decimal GatewayFees,
            decimal DeliveryFees,
            decimal OtherOperationalFees,
            decimal TotalExternalFees,
            decimal TotalRestaurantFees,
            decimal GrossRevenue,
            decimal NetRevenue,
            decimal FoodCost,
            decimal PackagingCost,
            decimal DeliveryCost,
            decimal PartnerCommission,
            decimal PaymentProcessingFee,
            decimal RestaurantCostShare,
            decimal ProviderCostShare,
            decimal LaborCost,
            decimal OperationalCost,
            decimal GrossProfit,
            decimal GrossMarginPercentage,
            decimal OperatingProfit,
            decimal OperatingMarginPercentage,
            decimal NetProfit,
            decimal NetMarginPercentage,
            decimal ProfitPerOrder,
            decimal ContributionMargin,
            decimal ContributionPercentage,
            string? CostSharingDetailsJson);
    }
}
