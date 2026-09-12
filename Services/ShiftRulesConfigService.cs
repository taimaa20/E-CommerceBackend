using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Caching;

namespace RestaurantPos.Api.Services
{
    /// <inheritdoc cref="IShiftRulesConfigService"/>
    public class ShiftRulesConfigService : IShiftRulesConfigService
    {
        private readonly PosDbContext _context;
        private readonly ICacheService _cache;
        private readonly IConfigAuditService _audit;
        private readonly ILogger<ShiftRulesConfigService> _logger;

        private static readonly TimeSpan CacheSliding  = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan CacheAbsolute = TimeSpan.FromMinutes(5);

        public ShiftRulesConfigService(
            PosDbContext context,
            ICacheService cache,
            IConfigAuditService audit,
            ILogger<ShiftRulesConfigService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cache   = cache   ?? throw new ArgumentNullException(nameof(cache));
            _audit   = audit   ?? throw new ArgumentNullException(nameof(audit));
            _logger  = logger  ?? throw new ArgumentNullException(nameof(logger));
        }

        private static string CacheKey(Guid tenantId) => $"shift-rules-config:{tenantId:N}";

        public async Task<ShiftRulesConfig?> GetForTenantAsync(Guid tenantId, CancellationToken ct)
        {
            if (tenantId == Guid.Empty) return null;

            return await _cache.GetOrCreateAsync(
                CacheKey(tenantId),
                async () => await _context.ShiftRulesConfigs
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantId && c.DeletedAt == null)
                    .FirstOrDefaultAsync(ct),
                slidingExpiration: CacheSliding,
                absoluteExpiration: CacheAbsolute,
                cancellationToken: ct);
        }

        public async Task<ShiftRulesConfig> UpsertAsync(
            Guid tenantId,
            ShiftRulesConfig draft,
            CancellationToken ct)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));
            if (draft is null)
                throw new ArgumentNullException(nameof(draft));

            var existing = await _context.ShiftRulesConfigs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.DeletedAt == null, ct);

            var previous = existing is null ? null : SnapshotOf(existing);

            if (existing is null)
            {
                existing = new ShiftRulesConfig { Id = Guid.NewGuid(), TenantId = tenantId };
                _context.ShiftRulesConfigs.Add(existing);
            }

            existing.ActiveShiftRule = draft.ActiveShiftRule;

            existing.RequireAllOrdersPaid      = draft.RequireAllOrdersPaid;
            existing.RequireAllOrdersReady     = draft.RequireAllOrdersReady;
            existing.RequireAllOrdersServed    = draft.RequireAllOrdersServed;
            existing.RequireAllOrdersCompleted = draft.RequireAllOrdersCompleted;

            existing.AllowPendingOrders               = draft.AllowPendingOrders;
            existing.AllowPreparingOrders             = draft.AllowPreparingOrders;
            existing.AllowReadyOrders                 = draft.AllowReadyOrders;
            existing.AllowPendingDeliveryOrders       = draft.AllowPendingDeliveryOrders;
            existing.AllowPendingCancellationRequests = draft.AllowPendingCancellationRequests;

            existing.AllowForcedShiftClose         = draft.AllowForcedShiftClose;
            existing.AllowShiftCloseWithOpenOrders = draft.AllowShiftCloseWithOpenOrders;

            await _context.SaveChangesAsync(ct);
            await _cache.RemoveAsync(CacheKey(tenantId), ct);

            await _audit.LogAsync(
                tenantId, ConfigAuditEventType.ShiftRulesChanged,
                previousValue: previous,
                newValue: SnapshotOf(existing),
                ct: ct);

            _logger.LogInformation(
                "Updated ShiftRulesConfig for tenant {TenantId}: ActiveShiftRule={Rule} ForceCloseAllowed={Force}",
                tenantId, existing.ActiveShiftRule, existing.AllowForcedShiftClose);

            return existing;
        }

        private static object SnapshotOf(ShiftRulesConfig c) => new
        {
            c.ActiveShiftRule,
            c.RequireAllOrdersPaid,
            c.RequireAllOrdersReady,
            c.RequireAllOrdersServed,
            c.RequireAllOrdersCompleted,
            c.AllowPendingOrders,
            c.AllowPreparingOrders,
            c.AllowReadyOrders,
            c.AllowPendingDeliveryOrders,
            c.AllowPendingCancellationRequests,
            c.AllowForcedShiftClose,
            c.AllowShiftCloseWithOpenOrders,
        };
    }
}
