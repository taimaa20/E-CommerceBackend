using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    /// <inheritdoc cref="IOrderNumberingConfigService"/>
    public class OrderNumberingConfigService : IOrderNumberingConfigService
    {
        private readonly PosDbContext _context;
        private readonly ICacheService _cache;
        private readonly IConfigAuditService _audit;
        private readonly ILogger<OrderNumberingConfigService> _logger;

        private static readonly TimeSpan CacheSliding  = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan CacheAbsolute = TimeSpan.FromMinutes(5);

        public OrderNumberingConfigService(
            PosDbContext context,
            ICacheService cache,
            IConfigAuditService audit,
            ILogger<OrderNumberingConfigService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cache   = cache   ?? throw new ArgumentNullException(nameof(cache));
            _audit   = audit   ?? throw new ArgumentNullException(nameof(audit));
            _logger  = logger  ?? throw new ArgumentNullException(nameof(logger));
        }

        private static string CacheKey(Guid tenantId) => $"order-numbering-config:{tenantId:N}";

        public async Task<OrderNumberingConfig?> GetForTenantAsync(Guid tenantId, CancellationToken ct)
        {
            if (tenantId == Guid.Empty) return null;

            return await _cache.GetOrCreateAsync(
                CacheKey(tenantId),
                async () => await _context.OrderNumberingConfigs
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantId && c.DeletedAt == null)
                    .FirstOrDefaultAsync(ct),
                slidingExpiration: CacheSliding,
                absoluteExpiration: CacheAbsolute,
                cancellationToken: ct);
        }

        public async Task<OrderNumberingConfig> UpsertAsync(
            Guid tenantId,
            OrderNumberingConfig draft,
            CancellationToken ct)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));
            if (draft is null)
                throw new ArgumentNullException(nameof(draft));

            var existing = await _context.OrderNumberingConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.DeletedAt == null, ct);

            // Snapshot before mutation so audit captures the real previous state.
            var previous = existing is null ? null : SnapshotOf(existing);

            if (existing is null)
            {
                existing = new OrderNumberingConfig { Id = Guid.NewGuid(), TenantId = tenantId };
                _context.OrderNumberingConfigs.Add(existing);
            }

            existing.ResetStrategy      = draft.ResetStrategy;
            existing.Scope              = draft.Scope;
            existing.Prefix             = string.IsNullOrWhiteSpace(draft.Prefix) ? null : draft.Prefix.Trim();
            existing.IncludeDate        = draft.IncludeDate;
            existing.IncludeMonth       = draft.IncludeMonth;
            existing.IncludeYear        = draft.IncludeYear;
            existing.IncludeShiftNumber = draft.IncludeShiftNumber;
            existing.IncludeBranchCode  = draft.IncludeBranchCode;
            existing.BranchCode         = string.IsNullOrWhiteSpace(draft.BranchCode) ? null : draft.BranchCode.Trim();

            await _context.SaveChangesAsync(ct);
            await _cache.RemoveAsync(CacheKey(tenantId), ct);

            await _audit.LogAsync(
                tenantId, ConfigAuditEventType.NumberingConfigChanged,
                previousValue: previous,
                newValue: SnapshotOf(existing),
                branchCode: existing.BranchCode,
                ct: ct);

            _logger.LogInformation(
                "Updated OrderNumberingConfig for tenant {TenantId}: Reset={Reset} Scope={Scope}",
                tenantId, existing.ResetStrategy, existing.Scope);

            return existing;
        }

        // Flat snapshot — avoids serializing audit fields like CreatedAt/UpdatedAt
        // that don't carry user-facing meaning in a config diff.
        private static object SnapshotOf(OrderNumberingConfig c) => new
        {
            c.ResetStrategy,
            c.Scope,
            c.Prefix,
            c.IncludeDate,
            c.IncludeMonth,
            c.IncludeYear,
            c.IncludeShiftNumber,
            c.IncludeBranchCode,
            c.BranchCode,
        };
    }
}
