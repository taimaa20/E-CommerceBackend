using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Exceptions;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Marketing.Domain;
using RestaurantPos.Api.Modules.Marketing.DTOs;
using RestaurantPos.Api.Modules.Marketing.Interfaces;
using RestaurantPos.Api.Modules.Marketing.Jobs;
using RestaurantPos.Api.Security;
using RestaurantPos.Api.Services;

namespace RestaurantPos.Api.Modules.Marketing.Services
{
    /// <inheritdoc cref="ICampaignService"/>
    public class CampaignService : ICampaignService
    {
        private readonly PosDbContext _context;
        private readonly ITenantResolver _tenant;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMarketingAuditLogger _audit;

        public CampaignService(
            PosDbContext context,
            ITenantResolver tenant,
            ICurrentUserAccessor currentUser,
            IMarketingAuditLogger audit)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public async Task<List<CampaignDto>> GetAllAsync(bool includeArchived, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            var campaigns = await _context.Campaigns
                .Include(c => c.TargetCustomers)
                .Where(c => c.TenantId == tenantId && (includeArchived || c.Status != CampaignStatus.Archived))
                .OrderByDescending(c => c.Priority).ThenByDescending(c => c.StartDate)
                .ToListAsync(ct);
            return campaigns.Select(MapToDto).ToList();
        }

        public async Task<CampaignDto> GetByIdAsync(Guid id, CancellationToken ct)
            => MapToDto(await LoadAsync(id, ct));

        public async Task<CampaignDto> CreateAsync(CampaignUpsertDto dto, CancellationToken ct)
        {
            Validate(dto);
            var tenantId = _tenant.GetTenantId();

            var campaign = new Campaign
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Status = CampaignStatus.Draft,
                CreatedByUserId = _currentUser.UserIdOrNull
            };
            Apply(campaign, dto, tenantId);

            _context.Campaigns.Add(campaign);
            _audit.Track(tenantId, null, MarketingAuditAction.Created,
                nameof(Campaign), campaign.Id, null, $"name={campaign.Name};type={campaign.Type}");

            await _context.SaveChangesAsync(ct);
            return MapToDto(campaign);
        }

        public async Task<CampaignDto> UpdateAsync(Guid id, CampaignUpsertDto dto, CancellationToken ct)
        {
            Validate(dto);
            var tenantId = _tenant.GetTenantId();

            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var campaign = await LoadAsync(id, ct);
            if (campaign.Status == CampaignStatus.Archived)
                throw new ValidationException("Archived campaigns cannot be edited.");

            _context.CampaignCustomers.RemoveRange(campaign.TargetCustomers);
            Apply(campaign, dto, tenantId);
            campaign.UpdatedByUserId = _currentUser.UserIdOrNull;

            _audit.Track(tenantId, campaign.BranchId, MarketingAuditAction.Updated,
                nameof(Campaign), campaign.Id, null, $"name={campaign.Name};type={campaign.Type}");

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return MapToDto(await LoadAsync(id, ct));
        }

        public async Task<CampaignDto> ChangeStatusAsync(Guid id, CampaignStatus target, CancellationToken ct)
        {
            var campaign = await LoadAsync(id, ct);
            if (!CampaignStatusPolicy.CanTransition(campaign.Status, target))
                throw new ValidationException($"Cannot transition campaign from {campaign.Status} to {target}.");

            var previous = campaign.Status;
            campaign.Status = target;
            if (target == CampaignStatus.Archived) campaign.IsActive = false;
            campaign.UpdatedByUserId = _currentUser.UserIdOrNull;

            _audit.Track(campaign.TenantId, campaign.BranchId, MarketingAuditAction.Updated,
                nameof(Campaign), campaign.Id, null, $"status:{previous}->{target}");

            await _context.SaveChangesAsync(ct);
            return MapToDto(campaign);
        }

        public async Task<CampaignDashboardDto> GetDashboardAsync(CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();

            // (1) Campaign status counts — one grouped query (includes archived for "total").
            var statusCounts = await _context.Campaigns
                .Where(c => c.TenantId == tenantId)
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

            // (2) The non-archived campaign rows (small set).
            var campaigns = await _context.Campaigns
                .Where(c => c.TenantId == tenantId && c.Status != CampaignStatus.Archived)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync(ct);

            // (3) Participants + points per campaign — one grouped query over the ledger.
            var stats = await _context.WalletTransactions
                .Where(t => t.Source == WalletTransactionSource.Campaign && t.CampaignId != null)
                .GroupBy(t => t.CampaignId!.Value)
                .Select(g => new
                {
                    CampaignId = g.Key,
                    Participants = g.Select(t => t.CustomerId).Distinct().Count(),
                    PointsIssued = g.Sum(t => (decimal?)t.Points) ?? 0m
                })
                .ToDictionaryAsync(x => x.CampaignId, ct);

            // (4) Audience sizes for ALL campaigns in a fixed number of grouped queries (no N+1).
            var audience = await ResolveAudienceSizesAsync(campaigns, tenantId, ct);

            var items = campaigns.Select(c =>
            {
                stats.TryGetValue(c.Id, out var s);
                var participants = s?.Participants ?? 0;
                var aud = audience.GetValueOrDefault(c.Id, 0);
                return new CampaignDashboardItemDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Status = c.Status,
                    Type = c.Type,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    TargetType = c.TargetType,
                    AudienceSize = aud,
                    Participants = participants,
                    PointsIssued = s?.PointsIssued ?? 0m,
                    ConversionRate = aud > 0 ? Math.Round((decimal)participants / aud, 4) : 0m
                };
            }).ToList();

            var totalParticipants = items.Sum(i => i.Participants);
            var totalPoints = items.Sum(i => i.PointsIssued);
            var activeItems = items.Where(i => i.Status == CampaignStatus.Active).ToList();
            var eligible = activeItems.Sum(i => i.AudienceSize);
            var participating = activeItems.Sum(i => i.Participants);

            var summary = new CampaignKpisDto
            {
                TotalCampaigns = statusCounts.Values.Sum(),
                ActiveCampaigns = statusCounts.GetValueOrDefault(CampaignStatus.Active),
                ScheduledCampaigns = statusCounts.GetValueOrDefault(CampaignStatus.Scheduled),
                ExpiredCampaigns = statusCounts.GetValueOrDefault(CampaignStatus.Expired),
                TotalParticipants = totalParticipants,
                TotalPointsIssued = totalPoints,
                AverageParticipantsPerCampaign = items.Count > 0 ? Math.Round((decimal)totalParticipants / items.Count, 2) : 0m,
                AveragePointsPerCampaign = items.Count > 0 ? Math.Round(totalPoints / items.Count, 2) : 0m,
                EligibleCustomers = eligible,
                ParticipatingCustomers = participating,
                ParticipationRate = eligible > 0 ? Math.Round((decimal)participating / eligible, 4) : 0m
            };

            return new CampaignDashboardDto { Summary = summary, Campaigns = items };
        }

        /// <summary>
        /// Computes audience size for every campaign in a fixed number of grouped queries (one per
        /// target-type plus a tier lookup) — never one query per campaign.
        /// </summary>
        private async Task<Dictionary<Guid, int>> ResolveAudienceSizesAsync(
            List<Campaign> campaigns, Guid tenantId, CancellationToken ct)
        {
            var result = new Dictionary<Guid, int>(campaigns.Count);

            int? totalCustomers = campaigns.Any(c => c.TargetType == CampaignTargetType.All)
                ? await _context.Customers.CountAsync(x => x.TenantId == tenantId, ct)
                : null;

            var segIds = campaigns.Where(c => c.TargetType == CampaignTargetType.Segment && c.TargetSegmentId != null)
                .Select(c => c.TargetSegmentId!.Value).Distinct().ToList();
            var segCounts = segIds.Count == 0
                ? new Dictionary<Guid, int>()
                : await _context.SegmentMembers.Where(m => segIds.Contains(m.SegmentId))
                    .GroupBy(m => m.SegmentId).Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

            var listIds = campaigns.Where(c => c.TargetType == CampaignTargetType.CustomerList).Select(c => c.Id).ToList();
            var listCounts = listIds.Count == 0
                ? new Dictionary<Guid, int>()
                : await _context.CampaignCustomers.Where(cc => listIds.Contains(cc.CampaignId))
                    .GroupBy(cc => cc.CampaignId).Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

            var tierIds = campaigns.Where(c => c.TargetType == CampaignTargetType.Tier && c.TargetTierId != null)
                .Select(c => c.TargetTierId!.Value).Distinct().ToList();
            var tierEnum = new Dictionary<Guid, CustomerTier>();
            var tierCustomerCounts = new Dictionary<CustomerTier, int>();
            if (tierIds.Count > 0)
            {
                tierEnum = await _context.LoyaltyTiers.Where(t => tierIds.Contains(t.Id))
                    .Select(t => new { t.Id, t.LegacyTierEnum })
                    .ToDictionaryAsync(x => x.Id, x => x.LegacyTierEnum, ct);
                var neededEnums = tierEnum.Values.Distinct().ToList();
                tierCustomerCounts = await _context.Customers
                    .Where(x => x.TenantId == tenantId && neededEnums.Contains(x.Tier))
                    .GroupBy(x => x.Tier).Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            }

            foreach (var c in campaigns)
            {
                result[c.Id] = c.TargetType switch
                {
                    CampaignTargetType.All => totalCustomers ?? 0,
                    CampaignTargetType.Segment => c.TargetSegmentId is { } sid ? segCounts.GetValueOrDefault(sid) : 0,
                    CampaignTargetType.CustomerList => listCounts.GetValueOrDefault(c.Id),
                    CampaignTargetType.Tier => c.TargetTierId is { } tid && tierEnum.TryGetValue(tid, out var le)
                        ? tierCustomerCounts.GetValueOrDefault(le) : 0,
                    _ => 0
                };
            }
            return result;
        }

        private async Task<Campaign> LoadAsync(Guid id, CancellationToken ct)
        {
            var tenantId = _tenant.GetTenantId();
            return await _context.Campaigns
                .Include(c => c.TargetCustomers)
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Campaign was not found.");
        }

        private void Apply(Campaign c, CampaignUpsertDto dto, Guid tenantId)
        {
            c.Name = dto.Name;
            c.NameAr = dto.NameAr;
            c.Description = dto.Description;
            c.DescriptionAr = dto.DescriptionAr;
            c.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
            c.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
            c.Priority = dto.Priority;
            c.IsActive = dto.IsActive;
            c.Type = dto.Type;
            c.TargetType = dto.TargetType;
            c.TargetSegmentId = dto.TargetType == CampaignTargetType.Segment ? dto.TargetSegmentId : null;
            c.TargetTierId = dto.TargetType == CampaignTargetType.Tier ? dto.TargetTierId : null;
            c.BonusPoints = dto.BonusPoints;
            c.SpendThreshold = dto.SpendThreshold;
            c.Multiplier = dto.Multiplier;
            c.PointsPerCurrencyUnit = dto.PointsPerCurrencyUnit;
            c.OrderCountThreshold = dto.OrderCountThreshold;
            c.ExpirationMode = dto.ExpirationMode;
            c.ExpirationValue = dto.ExpirationValue;

            if (dto.TargetType == CampaignTargetType.CustomerList)
                foreach (var customerId in dto.TargetCustomerIds.Distinct())
                    c.TargetCustomers.Add(new CampaignCustomer
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CampaignId = c.Id,
                        CustomerId = customerId,
                        CreatedByUserId = _currentUser.UserIdOrNull
                    });
        }

        private static void Validate(CampaignUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ValidationException("Campaign name is required.");
            if (dto.EndDate <= dto.StartDate)
                throw new ValidationException("End date must be after start date.");

            switch (dto.Type)
            {
                case CampaignType.BonusPoints when dto.BonusPoints is not > 0:
                    throw new ValidationException("Bonus-points campaigns require BonusPoints > 0.");
                case CampaignType.Multiplier when dto.Multiplier is not > 1m || dto.PointsPerCurrencyUnit is not > 0:
                    throw new ValidationException("Multiplier campaigns require Multiplier > 1 and PointsPerCurrencyUnit > 0.");
                case CampaignType.FixedReward when dto.OrderCountThreshold is not > 0 || dto.BonusPoints is not > 0:
                    throw new ValidationException("Fixed-reward campaigns require OrderCountThreshold > 0 and BonusPoints > 0.");
            }

            switch (dto.TargetType)
            {
                case CampaignTargetType.Segment when dto.TargetSegmentId is null:
                    throw new ValidationException("Segment-targeted campaigns require a segment.");
                case CampaignTargetType.Tier when dto.TargetTierId is null:
                    throw new ValidationException("Tier-targeted campaigns require a tier.");
                case CampaignTargetType.CustomerList when dto.TargetCustomerIds.Count == 0:
                    throw new ValidationException("Customer-list campaigns require at least one customer.");
            }

            if (dto.ExpirationMode != PointsExpirationMode.Never && dto.ExpirationValue is not > 0)
                throw new ValidationException("A non-Never expiration requires ExpirationValue > 0.");
        }

        private static CampaignDto MapToDto(Campaign c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            NameAr = c.NameAr,
            Description = c.Description,
            DescriptionAr = c.DescriptionAr,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Status = c.Status,
            Priority = c.Priority,
            IsActive = c.IsActive,
            Type = c.Type,
            TargetType = c.TargetType,
            TargetSegmentId = c.TargetSegmentId,
            TargetTierId = c.TargetTierId,
            BonusPoints = c.BonusPoints,
            SpendThreshold = c.SpendThreshold,
            Multiplier = c.Multiplier,
            PointsPerCurrencyUnit = c.PointsPerCurrencyUnit,
            OrderCountThreshold = c.OrderCountThreshold,
            ExpirationMode = c.ExpirationMode,
            ExpirationValue = c.ExpirationValue,
            TargetCustomerIds = c.TargetCustomers.Select(tc => tc.CustomerId).ToList(),
            CreatedAt = c.CreatedAt,
            EffectiveBonusRate = c.Type == CampaignType.Multiplier
                ? Jobs.CampaignAwardCalculator.EffectiveMultiplierRate(c.Multiplier, c.PointsPerCurrencyUnit)
                : null
        };
    }
}
