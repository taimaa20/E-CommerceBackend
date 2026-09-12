using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.CustomerMobile.DTOs;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.CustomerMobile.Services
{
    public class CustomerLoyaltyService : ICustomerLoyaltyService
    {
        private readonly PosDbContext _context;
        private readonly ICustomerMobileContext _mobileContext;
        private readonly ISettingsService _settingsService;
        private readonly IWalletQueryService _walletQuery;
        private readonly ILogger<CustomerLoyaltyService> _logger;

        public CustomerLoyaltyService(
            PosDbContext context,
            ICustomerMobileContext mobileContext,
            ISettingsService settingsService,
            IWalletQueryService walletQuery,
            ILogger<CustomerLoyaltyService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mobileContext = mobileContext ?? throw new ArgumentNullException(nameof(mobileContext));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _walletQuery = walletQuery ?? throw new ArgumentNullException(nameof(walletQuery));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CustomerLoyaltyWalletDto> GetWalletAsync(CancellationToken ct)
        {
            var data = await LoadLoyaltyContextAsync(ct);
            var tier = data.Customer?.Tier ?? CustomerTier.Standard;

            // Wallet is keyed by the CRM customer resolved from the authenticated account's phone —
            // a customer can therefore only ever read their own wallet.
            var wallet = data.Customer is null ? null : await _walletQuery.GetWalletAsync(data.Customer.Id, ct);
            var available = wallet?.AvailablePoints ?? data.Customer?.LoyaltyPoints ?? 0m;

            return new CustomerLoyaltyWalletDto
            {
                CustomerId = data.Account.Id,
                AvailablePoints = available,
                PendingPoints = wallet?.PendingPoints ?? 0m,
                LifetimePoints = wallet?.LifetimePoints ?? 0m,
                RedeemedPoints = wallet?.RedeemedPoints ?? 0m,
                ExpiredPoints = wallet?.ExpiredPoints ?? 0m,
                AvailableBalance = RoundMoney(available * data.Settings.LoyaltyPointValue),
                CurrencyCode = wallet?.CurrencyCode ?? "JOD",
                Tier = tier.ToString(),
                TierAr = MapTierAr(tier),
                Status = wallet?.Status ?? "Active"
            };
        }

        public async Task<PaginatedResponse<CustomerLoyaltyTransactionDto>> GetTransactionsAsync(
            int page, int pageSize, CancellationToken ct)
        {
            var data = await LoadLoyaltyContextAsync(ct);
            if (data.Customer is null)
                return new PaginatedResponse<CustomerLoyaltyTransactionDto>
                {
                    Items = new List<CustomerLoyaltyTransactionDto>(),
                    TotalCount = 0,
                    PageNumber = page < 1 ? 1 : page,
                    PageSize = pageSize
                };

            var history = await _walletQuery.GetHistoryAsync(data.Customer.Id, page, pageSize, ct);
            return new PaginatedResponse<CustomerLoyaltyTransactionDto>
            {
                Items = history.Items.Select(t => new CustomerLoyaltyTransactionDto
                {
                    Id = t.Id,
                    Type = t.Type,
                    Points = t.Points,
                    BalanceAfter = t.BalanceAfter,
                    Status = t.Status,
                    Source = t.Source,
                    Reason = t.Reason,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt
                }).ToList(),
                TotalCount = history.TotalCount,
                PageNumber = history.PageNumber,
                PageSize = history.PageSize
            };
        }

        public async Task<CustomerLoyaltySummaryDto> GetSummaryAsync(CancellationToken ct)
        {
            var data = await LoadLoyaltyContextAsync(ct);
            var points = data.Customer?.LoyaltyPoints ?? 0m;
            var tier = data.Customer?.Tier ?? CustomerTier.Standard;
            var nextTier = ResolveNextTier(points, data.Settings);

            return new CustomerLoyaltySummaryDto
            {
                CustomerId = data.Account.Id,
                AvailablePoints = points,
                AvailableBalance = RoundMoney(points * data.Settings.LoyaltyPointValue),
                Tier = tier.ToString(),
                TierAr = MapTierAr(tier),
                NextTier = nextTier?.Tier.ToString(),
                PointsToNextTier = nextTier?.PointsToNext ?? 0m,
                LastUpdated = data.Settings.UpdatedAt
            };
        }

        public async Task<CustomerLoyaltyCalculationDto> CalculateAsync(
            CustomerLoyaltyCalculateRequest request,
            CancellationToken ct)
        {
            var data = await LoadLoyaltyContextAsync(ct);
            var availablePoints = data.Customer?.LoyaltyPoints ?? 0m;
            ValidateCalculation(request, availablePoints, data.Settings);

            var discount = RoundMoney(request.PointsToUse * data.Settings.LoyaltyPointValue);
            var now = DateTime.UtcNow;
            AddAuditLog(data.Account, request, discount, now);

            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Customer {CustomerId} calculated loyalty redemption", data.Account.Id);
            return new CustomerLoyaltyCalculationDto
            {
                PointsUsed = request.PointsToUse,
                DiscountAmount = discount,
                RemainingPoints = availablePoints - request.PointsToUse,
                FinalTotal = RoundMoney(request.OrderAmount - discount)
            };
        }

        public async Task<List<CustomerRewardDto>> GetRewardsAsync(CancellationToken ct)
        {
            var data = await LoadLoyaltyContextAsync(ct);
            var now = DateTime.UtcNow;
            var points = data.Customer?.LoyaltyPoints ?? 0m;

            var rewards = await _context.Rewards.AsNoTracking()
                .Where(r => r.TenantId == data.Account.TenantId && r.IsActive && r.StartDate <= now && r.EndDate > now)
                .OrderBy(r => r.PointsRequired)
                .ToListAsync(ct);

            var productIds = rewards.Where(r => r.ProductId.HasValue).Select(r => r.ProductId!.Value).Distinct().ToList();
            var productNames = productIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Name }).ToDictionaryAsync(p => p.Id, p => p.Name, ct);

            return rewards.Select(r => new CustomerRewardDto
            {
                Id = r.Id,
                Name = r.Name,
                NameAr = r.NameAr,
                Description = r.Description,
                DescriptionAr = r.DescriptionAr,
                PointsRequired = r.PointsRequired,
                Type = r.Type.ToString(),
                RewardValue = r.RewardValue,
                MaxDiscountAmount = r.MaxDiscountAmount,
                ProductId = r.ProductId,
                ProductName = r.ProductId is { } pid ? productNames.GetValueOrDefault(pid) : null,
                Affordable = points >= r.PointsRequired
            }).ToList();
        }

        public async Task<PaginatedResponse<CustomerRewardRedemptionDto>> GetRewardRedemptionsAsync(
            int page, int pageSize, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

            var data = await LoadLoyaltyContextAsync(ct);
            if (data.Customer is null)
                return new PaginatedResponse<CustomerRewardRedemptionDto>
                {
                    Items = new List<CustomerRewardRedemptionDto>(),
                    TotalCount = 0,
                    PageNumber = page,
                    PageSize = pageSize
                };

            var query =
                from rd in _context.RewardRedemptions.AsNoTracking()
                    .Where(x => x.CustomerId == data.Customer.Id && x.TenantId == data.Account.TenantId)
                join rw in _context.Rewards.AsNoTracking() on rd.RewardId equals rw.Id into rwj
                from rw in rwj.DefaultIfEmpty()
                orderby rd.RedeemedAt descending
                select new { rd, rw };

            var total = await query.CountAsync(ct);
            var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            return new PaginatedResponse<CustomerRewardRedemptionDto>
            {
                Items = rows.Select(x => new CustomerRewardRedemptionDto
                {
                    Id = x.rd.Id,
                    RewardName = x.rw == null ? "—" : x.rw.Name,
                    Type = x.rd.RewardTypeSnapshot.ToString(),
                    PointsUsed = x.rd.PointsUsed,
                    DiscountAmount = x.rd.DiscountAmountSnapshot ?? 0m,
                    RedeemedAt = x.rd.RedeemedAt
                }).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        private async Task<LoyaltyContext> LoadLoyaltyContextAsync(CancellationToken ct)
        {
            var customerId = _mobileContext.GetCustomerId();
            var account = await _context.CustomerAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new NotFoundException("Customer profile was not found.");

            var crmCustomer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PhoneNumber == account.MobileNumber, ct);

            var settings = await _settingsService.GetSettingsAsync(account.TenantId, ct);
            return new LoyaltyContext(account, crmCustomer, settings);
        }

        private static void ValidateCalculation(
            CustomerLoyaltyCalculateRequest request,
            decimal availablePoints,
            SystemSettings settings)
        {
            if (!settings.EnableLoyalty)
                throw new ValidationException("Loyalty is disabled.");

            if (request.PointsToUse == 0)
                return;

            ValidateRedemptionPoints(request.PointsToUse, availablePoints, settings);
            var discount = RoundMoney(request.PointsToUse * settings.LoyaltyPointValue);
            if (discount <= 0 || discount > request.OrderAmount)
                throw new ValidationException("Loyalty discount is invalid for this order.");
        }

        private static void ValidateRedemptionPoints(
            decimal pointsToUse,
            decimal availablePoints,
            SystemSettings settings)
        {
            if (settings.LoyaltyPointValue <= 0)
                throw new ValidationException("Loyalty redemption is not configured.");

            if (pointsToUse > availablePoints)
                throw new ValidationException("Requested loyalty points exceed available points.");

            if (settings.LoyaltyMinimumRedeemPoints.HasValue && pointsToUse < settings.LoyaltyMinimumRedeemPoints.Value)
                throw new ValidationException("Requested loyalty points are below the minimum redemption amount.");

            if (settings.LoyaltyMaximumRedeemPoints.HasValue && pointsToUse > settings.LoyaltyMaximumRedeemPoints.Value)
                throw new ValidationException("Requested loyalty points exceed the maximum redemption amount.");
        }

        private void AddAuditLog(
            CustomerAccount account,
            CustomerLoyaltyCalculateRequest request,
            decimal discount,
            DateTime now)
            => _context.CustomerMobileAuditLogs.Add(new CustomerMobileAuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = account.TenantId,
                CustomerId = account.Id,
                Action = CustomerMobileAuditAction.LoyaltyCalculated,
                Metadata = $"OrderAmount={request.OrderAmount};PointsToUse={request.PointsToUse};Discount={discount}",
                ActionAt = now
            });

        private static LoyaltyNextTier? ResolveNextTier(decimal points, SystemSettings settings)
            => GetThresholds(settings)
                .Where(t => t.Points > points)
                .OrderBy(t => t.Points)
                .Select(t => new LoyaltyNextTier(t.Tier, t.Points - points))
                .FirstOrDefault();

        private static IEnumerable<LoyaltyThreshold> GetThresholds(SystemSettings settings)
        {
            if (settings.LoyaltyBronzeThreshold.HasValue)
                yield return new LoyaltyThreshold(CustomerTier.Bronze, settings.LoyaltyBronzeThreshold.Value);
            if (settings.LoyaltySilverThreshold.HasValue)
                yield return new LoyaltyThreshold(CustomerTier.Silver, settings.LoyaltySilverThreshold.Value);
            if (settings.LoyaltyGoldThreshold.HasValue)
                yield return new LoyaltyThreshold(CustomerTier.Gold, settings.LoyaltyGoldThreshold.Value);
            if (settings.LoyaltyVipThreshold.HasValue)
                yield return new LoyaltyThreshold(CustomerTier.VIP, settings.LoyaltyVipThreshold.Value);
        }

        private static string MapTierAr(CustomerTier tier)
            => tier switch
            {
                CustomerTier.Bronze => "برونزي",
                CustomerTier.Silver => "فضي",
                CustomerTier.Gold => "ذهبي",
                CustomerTier.VIP => "مميز",
                _ => "عادي"
            };

        private static decimal RoundMoney(decimal value)
            => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private sealed record LoyaltyContext(CustomerAccount Account, Customer? Customer, SystemSettings Settings);
        private sealed record LoyaltyThreshold(CustomerTier Tier, decimal Points);
        private sealed record LoyaltyNextTier(CustomerTier Tier, decimal PointsToNext);
    }
}
