using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="IEarningEngine"/>
    public class EarningEngine : IEarningEngine
    {
        private readonly PosDbContext _context;
        private readonly IWalletService _wallet;
        private readonly ILogger<EarningEngine> _logger;

        public EarningEngine(PosDbContext context, IWalletService wallet, ILogger<EarningEngine> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ApplyEarningForOrderAsync(Guid orderId, CancellationToken ct)
        {
            var order = await _context.Orders.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null)
            {
                _logger.LogWarning("Loyalty earning skipped: order {OrderId} not found.", orderId);
                return;
            }

            var settings = await _context.MarketingSettings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == order.TenantId, ct);
            if (settings is null || !settings.LoyaltyEnabled)
                return; // marketing not configured / loyalty disabled — legacy path remains authoritative

            var customer = await ResolveCustomerAsync(order, ct);
            if (customer is null)
                return; // anonymous order — nothing to earn against

            var rules = await LoadCurrentRulesAsync(order.TenantId, ct);
            if (rules.Count == 0)
                return;

            var earningContext = await BuildContextAsync(order, customer, ct);
            var tierMultiplier = await ResolveTierMultiplierAsync(order.TenantId, customer.Tier, ct);

            var awards = EarningCalculator.Evaluate(earningContext, rules, tierMultiplier, DateTime.UtcNow);
            if (awards.Count == 0)
                return;

            var snapshot = new OrderSnapshotData
            {
                CustomerPhone = order.CustomerPhone,
                OrderSource = order.OrderSource,
                Subtotal = order.Subtotal,
                Discount = order.DiscountAmount + order.VoucherDiscountAmount,
                Tax = order.TaxAmount,
                TotalAmount = order.TotalAmount
            };

            // Each award is a separate, idempotent credit keyed by (order, rule); replaying the
            // OrderPaid event re-runs this safely and never double-earns.
            foreach (var award in awards)
            {
                await _wallet.CreditAsync(new WalletCreditRequest
                {
                    CustomerId = customer.Id,
                    Points = award.Points,
                    Type = award.IsPending ? WalletTransactionType.EarnPendingApproved : WalletTransactionType.Earn,
                    Source = WalletTransactionSource.Order,
                    IsPending = award.IsPending,
                    IdempotencyKey = $"order:{order.Id}:rule:{award.RuleVersionId}",
                    ExpiresAt = award.ExpiresAt,
                    OrderId = order.Id,
                    RuleVersionId = award.RuleVersionId,
                    OrderSnapshot = snapshot with { EarnedPoints = award.Points }
                }, ct);
            }

            _logger.LogInformation(
                "Loyalty: credited {Count} award(s) for order {OrderId}, customer {CustomerId}.",
                awards.Count, order.Id, customer.Id);
        }

        /// <summary>Resolves the loyalty subject; auto-creates a CRM customer by phone when absent.</summary>
        private async Task<Customer?> ResolveCustomerAsync(Order order, CancellationToken ct)
        {
            if (order.CustomerId.HasValue)
            {
                return await _context.Customers.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Id == order.CustomerId.Value, ct);
            }

            if (string.IsNullOrWhiteSpace(order.CustomerPhone))
                return null;

            var phone = order.CustomerPhone.Trim();
            var existing = await _context.Customers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == order.TenantId && c.PhoneNumber == phone, ct);
            if (existing is not null)
                return existing;

            var created = new Customer
            {
                Id = Guid.NewGuid(),
                TenantId = order.TenantId,
                Name = phone,
                PhoneNumber = phone,
                Tier = CustomerTier.Standard,
                LastVisit = DateTime.UtcNow
            };
            _context.Customers.Add(created);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Loyalty: auto-created CRM customer {CustomerId} from order phone.", created.Id);
            return created;
        }

        private async Task<OrderEarningContext> BuildContextAsync(Order order, Customer customer, CancellationToken ct)
        {
            var lines = await _context.OrderItems.IgnoreQueryFilters()
                .Where(i => i.OrderId == order.Id && !i.IsComplimentary)
                .Select(i => new OrderLine(
                    i.ProductId,
                    i.Product.CategoryId,
                    i.LineTotalSnapshot ?? i.Price * i.Quantity))
                .ToListAsync(ct);

            var isFirstOrder = !await _context.WalletTransactions.IgnoreQueryFilters()
                .AnyAsync(t => t.CustomerId == customer.Id
                               && t.Source == WalletTransactionSource.Order
                               && t.OrderId != order.Id, ct);

            var now = DateTime.UtcNow;
            var isBirthday = customer.BirthDate is { } bd && bd.Month == now.Month && bd.Day == now.Day;

            return new OrderEarningContext
            {
                Total = order.TotalAmount,
                Subtotal = order.Subtotal,
                Discount = order.DiscountAmount + order.VoucherDiscountAmount,
                Tax = order.TaxAmount,
                Source = order.OrderSource,
                IsFirstOrder = isFirstOrder,
                IsBirthday = isBirthday,
                Lines = lines
            };
        }

        private async Task<decimal> ResolveTierMultiplierAsync(Guid tenantId, CustomerTier tier, CancellationToken ct)
        {
            var multiplier = await _context.LoyaltyTiers.IgnoreQueryFilters()
                .Where(t => t.TenantId == tenantId && t.IsActive && t.LegacyTierEnum == tier)
                .Select(t => (decimal?)t.EarnMultiplier)
                .FirstOrDefaultAsync(ct);
            return multiplier ?? 1m;
        }

        private Task<List<EarningRuleSnapshot>> LoadCurrentRulesAsync(Guid tenantId, CancellationToken ct)
            => _context.EarningRules.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId && r.IsCurrent && r.IsActive && r.BranchId == null)
                .Select(r => new EarningRuleSnapshot
                {
                    RuleVersionId = r.Id,
                    RuleType = r.RuleType,
                    PointsValue = r.PointsValue,
                    PointsPerCurrencyUnit = r.PointsPerCurrencyUnit,
                    Multiplier = r.Multiplier,
                    TargetCategoryId = r.TargetCategoryId,
                    TargetProductId = r.TargetProductId,
                    ChannelScope = r.ChannelScope,
                    ApprovalMode = r.ApprovalMode,
                    ApprovalDelayDays = r.ApprovalDelayDays,
                    ExpirationMode = r.ExpirationMode,
                    ExpirationValue = r.ExpirationValue,
                    Priority = r.Priority
                })
                .ToListAsync(ct);
    }
}
