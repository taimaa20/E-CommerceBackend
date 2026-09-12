using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="IRewardService"/>
    public class RewardService : IRewardService
    {
        private const int MaxPageSize = 100;

        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;
        private readonly IWalletService _wallet;
        private readonly ILogger<RewardService> _logger;

        public RewardService(
            PosDbContext context,
            ITenantResolver tenant,
            ICurrentUserAccessor currentUser,
            IMarketingAuditLogger audit,
            IWalletService wallet,
            ILogger<RewardService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<RewardDto>> GetAllAsync(bool includeInactive, bool isArabic, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var rewards = await _context.Rewards
                .Where(r => r.TenantId == tenantId && (includeInactive || r.IsActive))
                .OrderBy(r => r.PointsRequired)
                .ToListAsync(ct);
            return await MapManyAsync(rewards, isArabic, ct);
        }

        public async Task<RewardDto> GetByIdAsync(Guid id, bool isArabic, CancellationToken ct)
            => (await MapManyAsync(new[] { await LoadAsync(id, ct) }, isArabic, ct))[0];

        public async Task<RewardDto> CreateAsync(RewardUpsertDto dto, CancellationToken ct)
        {
            await ValidateAsync(dto, ct);
            var tenantId = _tenant.GetTenantId();

            var reward = new Reward { Id = Guid.NewGuid(), TenantId = tenantId, CreatedByUserId = _currentUser.UserIdOrNull };
            Apply(reward, dto);
            _context.Rewards.Add(reward);

            _audit.Track(tenantId, null, MarketingAuditAction.Created,
                nameof(Reward), reward.Id, null, $"name={reward.Name};type={reward.Type};points={reward.PointsRequired}");

            await _context.SaveChangesAsync(ct);
            return (await MapManyAsync(new[] { reward }, false, ct))[0];
        }

        public async Task<RewardDto> UpdateAsync(Guid id, RewardUpsertDto dto, CancellationToken ct)
        {
            await ValidateAsync(dto, ct);
            var reward = await LoadAsync(id, ct);
            Apply(reward, dto);
            reward.UpdatedByUserId = _currentUser.UserIdOrNull;

            _audit.Track(reward.TenantId, reward.BranchId, MarketingAuditAction.Updated,
                nameof(Reward), reward.Id, null, $"name={reward.Name};type={reward.Type}");

            await _context.SaveChangesAsync(ct);
            return (await MapManyAsync(new[] { reward }, false, ct))[0];
        }

        public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct)
        {
            var reward = await LoadAsync(id, ct);
            reward.IsActive = active;
            reward.UpdatedByUserId = _currentUser.UserIdOrNull;

            _audit.Track(reward.TenantId, reward.BranchId, MarketingAuditAction.Updated,
                nameof(Reward), reward.Id, null, active ? "activated" : "deactivated");

            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<RewardDto>> GetActiveCatalogAsync(bool isArabic, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var now = DateTime.UtcNow;
            var rewards = await _context.Rewards
                .Where(r => r.TenantId == tenantId && r.IsActive && r.StartDate <= now && r.EndDate > now)
                .OrderBy(r => r.PointsRequired)
                .ToListAsync(ct);
            return await MapManyAsync(rewards, isArabic, ct);
        }

        public async Task<RewardRedemptionResultDto> RedeemAsync(
            Guid rewardId, RewardRedeemRequest request, bool isArabic, CancellationToken ct)
        {
            var reward = await LoadAsync(rewardId, ct); // tenant-scoped
            var now = DateTime.UtcNow;

            // Fix 1: the customer must belong to the same (caller's) tenant as the reward.
            var customerInTenant = await _context.Customers.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(c => c.Id == request.CustomerId && c.TenantId == reward.TenantId, ct);
            if (!customerInTenant) throw new NotFoundException("Customer was not found.");

            if (!reward.IsActive) throw new ValidationException("Reward is not active.");
            if (now < reward.StartDate || now >= reward.EndDate) throw new ValidationException("Reward is not currently available.");
            if (reward.Type == RewardType.FreeProduct && reward.ProductId is null)
                throw new ValidationException("This reward is misconfigured (no product).");
            if (reward.Type == RewardType.PercentageDiscount && request.OrderTotal is not > 0)
                throw new ValidationException("Order total is required to redeem a percentage reward.");

            var discount = RewardBenefitCalculator.DiscountAmount(reward.Type, reward.RewardValue, reward.MaxDiscountAmount, request.OrderTotal);

            // Fix 2: deterministic idempotency key (one per client redeem action). The wallet engine
            // dedupes the deduction by this key, so double-click / retry deducts points only once.
            var keySuffix = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? Guid.NewGuid().ToString("N")
                : request.IdempotencyKey.Trim();
            var redeemKey = $"reward:{reward.Id}:{keySuffix}";

            // Points are deducted by the existing wallet engine (validates active wallet + sufficient points).
            var txn = await _wallet.RedeemAsync(new WalletRedeemRequest
            {
                CustomerId = request.CustomerId,
                Points = reward.PointsRequired,
                Source = WalletTransactionSource.Reward,
                IdempotencyKey = redeemKey,
                OrderId = request.OrderId,
                Reason = $"Reward: {reward.Name}",
                ReasonAr = reward.NameAr is null ? null : $"مكافأة: {reward.NameAr}"
            }, ct);

            // Fix 2: one redemption record per wallet transaction — a replay returns the existing one.
            var existing = await _context.RewardRedemptions.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(r => r.WalletTransactionId == txn.Id, ct);
            if (existing is not null)
                return BuildResult(reward, existing.Id, existing.DiscountAmountSnapshot ?? 0m, txn.BalanceAfter, isArabic);

            var redemption = new RewardRedemption
            {
                Id = Guid.NewGuid(),
                TenantId = reward.TenantId,
                BranchId = reward.BranchId,
                RewardId = reward.Id,
                CustomerId = request.CustomerId,
                PointsUsed = reward.PointsRequired,
                RewardTypeSnapshot = reward.Type,
                RewardValueSnapshot = reward.RewardValue,
                DiscountAmountSnapshot = discount,
                ProductIdSnapshot = reward.ProductId,
                WalletTransactionId = txn.Id,
                OrderId = request.OrderId,
                Status = RewardRedemptionStatus.Completed,
                RedeemedAt = now,
                CreatedByUserId = _currentUser.UserIdOrNull
            };
            _context.RewardRedemptions.Add(redemption);

            _audit.Track(reward.TenantId, reward.BranchId, MarketingAuditAction.PointsRedeemed,
                nameof(Reward), reward.Id, request.CustomerId,
                $"reward-redeemed;points={reward.PointsRequired};discount={discount}");

            // Fix 3: atomicity. The deduction committed in its own transaction; if persisting the
            // redemption record + audit fails, compensate by crediting the points back so the wallet
            // stays correct and no partial redemption remains.
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Reward] Redemption record failed for reward {RewardId}; compensating.", reward.Id);
                _context.ChangeTracker.Clear();
                await CompensateAsync(reward, request.CustomerId, redeemKey, ct);
                throw;
            }

            return BuildResult(reward, redemption.Id, discount, txn.BalanceAfter, isArabic);
        }

        private async Task CompensateAsync(Reward reward, Guid customerId, string redeemKey, CancellationToken ct)
        {
            try
            {
                await _wallet.CreditAsync(new WalletCreditRequest
                {
                    CustomerId = customerId,
                    Points = reward.PointsRequired,
                    Type = WalletTransactionType.Adjust,
                    Source = WalletTransactionSource.Manual,
                    IsPending = false,
                    IdempotencyKey = $"{redeemKey}:rollback",
                    Reason = "Reward redemption rolled back"
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Reward] Compensation credit failed for customer {CustomerId}; manual reconciliation required.", customerId);
            }
        }

        private RewardRedemptionResultDto BuildResult(Reward reward, Guid redemptionId, decimal discount, decimal remainingPoints, bool isArabic)
            => new()
            {
                RedemptionId = redemptionId,
                RewardId = reward.Id,
                RewardName = (isArabic ? reward.NameAr : reward.Name) ?? reward.Name,
                Type = reward.Type,
                PointsUsed = reward.PointsRequired,
                DiscountAmount = discount,
                FreeProductId = reward.Type == RewardType.FreeProduct ? reward.ProductId : null,
                FreeDelivery = reward.Type == RewardType.FreeDelivery,
                RemainingPoints = remainingPoints,
                Message = BuildMessage(reward.Type, discount, isArabic)
            };

        public async Task<PaginatedResponse<RewardRedemptionDto>> GetRedemptionsAsync(
            Guid customerId, int page, int pageSize, bool isArabic, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > MaxPageSize ? 20 : pageSize;
            var tenantId = _tenant.GetTenantId();

            var query =
                from rd in _context.RewardRedemptions.Where(x => x.CustomerId == customerId && x.TenantId == tenantId)
                join rw in _context.Rewards on rd.RewardId equals rw.Id into rwj
                from rw in rwj.DefaultIfEmpty()
                orderby rd.RedeemedAt descending
                select new { rd, rw };

            var total = await query.CountAsync(ct);
            var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            return new PaginatedResponse<RewardRedemptionDto>
            {
                Items = rows.Select(x => new RewardRedemptionDto
                {
                    Id = x.rd.Id,
                    RewardId = x.rd.RewardId,
                    RewardName = x.rw == null ? "—" : (isArabic ? x.rw.NameAr : x.rw.Name) ?? x.rw.Name,
                    Type = x.rd.RewardTypeSnapshot,
                    PointsUsed = x.rd.PointsUsed,
                    DiscountAmount = x.rd.DiscountAmountSnapshot ?? 0m,
                    RedeemedAt = x.rd.RedeemedAt,
                    Status = x.rd.Status.ToString()
                }).ToList(),
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }

        public async Task<RewardDashboardDto> GetDashboardAsync(CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();

            var activeRewards = await _context.Rewards.CountAsync(r => r.TenantId == tenantId && r.IsActive, ct);

            var redemptions = _context.RewardRedemptions.Where(r => r.TenantId == tenantId);
            var totalRedemptions = await redemptions.CountAsync(ct);
            var pointsRedeemed = await redemptions.SumAsync(r => (decimal?)r.PointsUsed, ct) ?? 0m;

            var topRaw = await redemptions
                .GroupBy(r => r.RewardId)
                .Select(g => new { RewardId = g.Key, Count = g.Count(), Points = g.Sum(x => (decimal?)x.PointsUsed) ?? 0m })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(ct);

            var ids = topRaw.Select(x => x.RewardId).ToList();
            var names = await _context.Rewards.Where(r => ids.Contains(r.Id))
                .Select(r => new { r.Id, r.Name }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);

            return new RewardDashboardDto
            {
                ActiveRewards = activeRewards,
                TotalRedemptions = totalRedemptions,
                PointsRedeemed = pointsRedeemed,
                MostRedeemed = topRaw.Select(x => new RewardTopItemDto
                {
                    RewardId = x.RewardId,
                    Name = names.GetValueOrDefault(x.RewardId, "—"),
                    Redemptions = x.Count,
                    PointsRedeemed = x.Points
                }).ToList()
            };
        }

        private async Task<Reward> LoadAsync(Guid id, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            return await _context.Rewards.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Reward was not found.");
        }

        private void Apply(Reward r, RewardUpsertDto dto)
        {
            r.Name = dto.Name;
            r.NameAr = dto.NameAr;
            r.Description = dto.Description;
            r.DescriptionAr = dto.DescriptionAr;
            r.PointsRequired = dto.PointsRequired;
            r.Type = dto.Type;
            r.RewardValue = dto.RewardValue;
            r.MaxDiscountAmount = dto.MaxDiscountAmount;
            r.ProductId = dto.Type == RewardType.FreeProduct ? dto.ProductId : null;
            r.IsActive = dto.IsActive;
            r.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
            r.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
        }

        private async Task ValidateAsync(RewardUpsertDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("Reward name is required.");
            if (dto.PointsRequired <= 0) throw new ValidationException("Points required must be greater than zero.");
            if (dto.EndDate <= dto.StartDate) throw new ValidationException("End date must be after start date.");

            switch (dto.Type)
            {
                case RewardType.FixedDiscount when dto.RewardValue is not > 0:
                    throw new ValidationException("Fixed-discount rewards require a positive value.");
                case RewardType.PercentageDiscount when dto.RewardValue is not > 0 or > 100:
                    throw new ValidationException("Percentage rewards require a value between 0 and 100.");
                case RewardType.FreeProduct when dto.ProductId is null:
                    throw new ValidationException("Free-product rewards require a product.");
            }

            if (dto.Type == RewardType.FreeProduct && dto.ProductId is { } pid)
            {
                var tenantId = _tenant.GetTenantId();
                var exists = await _context.Products.AnyAsync(p => p.Id == pid && p.TenantId == tenantId, ct);
                if (!exists) throw new ValidationException("Selected product was not found.");
            }
        }

        private async Task<List<RewardDto>> MapManyAsync(IReadOnlyCollection<Reward> rewards, bool isArabic, CancellationToken ct)
        {
            var productIds = rewards.Where(r => r.ProductId.HasValue).Select(r => r.ProductId!.Value).Distinct().ToList();
            var productNames = productIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.Products.Where(p => productIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Name, p.NameAr })
                    .ToDictionaryAsync(p => p.Id, p => (isArabic ? p.NameAr : p.Name) ?? p.Name, ct);

            return rewards.Select(r => new RewardDto
            {
                Id = r.Id,
                Name = r.Name,
                NameAr = r.NameAr,
                Description = r.Description,
                DescriptionAr = r.DescriptionAr,
                PointsRequired = r.PointsRequired,
                Type = r.Type,
                RewardValue = r.RewardValue,
                MaxDiscountAmount = r.MaxDiscountAmount,
                ProductId = r.ProductId,
                ProductName = r.ProductId is { } pid ? productNames.GetValueOrDefault(pid) : null,
                IsActive = r.IsActive,
                StartDate = r.StartDate,
                EndDate = r.EndDate
            }).ToList();
        }

        private static string BuildMessage(RewardType type, decimal discount, bool ar) => type switch
        {
            RewardType.FixedDiscount or RewardType.PercentageDiscount =>
                ar ? $"خصم {discount} — طبّقه عبر آلية الخصم." : $"{discount} discount — apply via the discount mechanism.",
            RewardType.FreeProduct => ar ? "منتج مجاني — أضِفه للطلب." : "Free product — add it to the order.",
            RewardType.FreeDelivery => ar ? "توصيل مجاني — ألغِ رسوم التوصيل." : "Free delivery — waive the delivery fee.",
            _ => ar ? "تم الاستبدال." : "Redeemed."
        };
    }
}
