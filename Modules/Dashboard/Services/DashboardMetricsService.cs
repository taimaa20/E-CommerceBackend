using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.DTOs;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Audit;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Financial;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Inventory;
using RestaurantPos.Api.Modules.Dashboard.DTOs.Operations;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Modules.Dashboard.Security;
using RestaurantPos.Api.Modules.Dashboard.Services.Audit;
using RestaurantPos.Api.Modules.Dashboard.Services.Caching;
using RestaurantPos.Api.Modules.Dashboard.Services.Financial;
using RestaurantPos.Api.Modules.Dashboard.Services.Inventory;
using RestaurantPos.Api.Modules.Dashboard.Services.Operational;

namespace RestaurantPos.Api.Modules.Dashboard.Services
{
    /// <inheritdoc />
    public sealed class DashboardMetricsService : IDashboardMetricsService
    {
        private readonly IOperationalMetricsService _ops;
        private readonly IFinancialAnalyticsService _fin;
        private readonly IInventoryAnalyticsService _inv;
        private readonly IAuditAnalyticsService     _aud;
        private readonly IDashboardCacheService     _cache;
        private readonly IDashboardFilterContext    _ctx;
        private readonly PosDbContext               _db;

        public DashboardMetricsService(
            IOperationalMetricsService ops,
            IFinancialAnalyticsService fin,
            IInventoryAnalyticsService inv,
            IAuditAnalyticsService     aud,
            IDashboardCacheService     cache,
            IDashboardFilterContext    ctx,
            PosDbContext               db)
        {
            _ops   = ops   ?? throw new ArgumentNullException(nameof(ops));
            _fin   = fin   ?? throw new ArgumentNullException(nameof(fin));
            _inv   = inv   ?? throw new ArgumentNullException(nameof(inv));
            _aud   = aud   ?? throw new ArgumentNullException(nameof(aud));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _ctx   = ctx   ?? throw new ArgumentNullException(nameof(ctx));
            _db    = db    ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<OperationsSnapshotDto> GetOperationsAsync(CancellationToken ct)
            => (await _cache.GetOrCreateAsync(DashboardScope.Operations, "snapshot", _ctx.Filter,
                () => _ops.BuildSnapshotAsync(ct), TtlFor(DashboardScope.Operations), ct))!;

        public async Task<FinancialSnapshotDto> GetFinancialAsync(CancellationToken ct)
            => (await _cache.GetOrCreateAsync(DashboardScope.Financial, "snapshot", _ctx.Filter,
                () => _fin.BuildSnapshotAsync(ct), TtlFor(DashboardScope.Financial), ct))!;

        public async Task<CommissionAnalysisDto> GetFinancialCommissionAnalysisAsync(CancellationToken ct)
            => (await _cache.GetOrCreateAsync(DashboardScope.Financial, "commission-analysis", _ctx.Filter,
                () => _fin.BuildCommissionAnalysisAsync(ct), TtlFor(DashboardScope.Financial), ct))!;

        public async Task<InventorySnapshotDto> GetInventoryAsync(CancellationToken ct)
            => (await _cache.GetOrCreateAsync(DashboardScope.Inventory, "snapshot", _ctx.Filter,
                () => _inv.BuildSnapshotAsync(ct), TtlFor(DashboardScope.Inventory), ct))!;

        public async Task<AuditSnapshotDto> GetAuditAsync(CancellationToken ct)
            => (await _cache.GetOrCreateAsync(DashboardScope.Audit, "snapshot", _ctx.Filter,
                () => _aud.BuildSnapshotAsync(ct), TtlFor(DashboardScope.Audit), ct))!;

        public async Task<DashboardFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct)
        {
            // Order BEFORE Select on a raw column — EF Core sometimes fails to
            // map "OrderBy(record.PropertyAfterProjection)" back to the original
            // column expression. Pulling raw anonymous types and projecting to
            // the DTO in memory is bulletproof and trivially cheap for these
            // small lookup lists.
            var rawCashiers = await _db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Cashier || u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin || u.Role == UserRole.Manager)
                .OrderBy(u => u.FullName ?? u.Username)
                .Select(u => new { u.Id, Name = u.FullName ?? u.Username, NameAr = u.FullNameAr })
                .ToListAsync(ct);
            var cashiers = rawCashiers.Select(c => new EmployeeOptionDto(c.Id, c.Name, c.NameAr)).ToList();

            var rawWaiters = await _db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Waiter)
                .OrderBy(u => u.FullName ?? u.Username)
                .Select(u => new { u.Id, Name = u.FullName ?? u.Username, NameAr = u.FullNameAr })
                .ToListAsync(ct);
            var waiters = rawWaiters.Select(c => new EmployeeOptionDto(c.Id, c.Name, c.NameAr)).ToList();

            var rawCategories = await _db.Categories.AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.NameAr })
                .ToListAsync(ct);
            var categories = rawCategories.Select(c => new CategoryOptionDto(c.Id, c.Name, c.NameAr)).ToList();

            var paymentMethods = await GetPaymentMethodOptionsAsync(ct);
            var partners = await GetPartnerOptionsAsync(ct);

            var rawWarehouses = await _db.Warehouses.AsNoTracking()
                .OrderByDescending(w => w.IsActive)
                .ThenBy(w => w.Type)
                .ThenBy(w => w.Name)
                .Select(w => new { w.Id, w.Name, w.NameAr, w.IsActive })
                .ToListAsync(ct);
            var warehouses = rawWarehouses
                .Select(w => new WarehouseOptionDto(w.Id, w.Name, w.NameAr, w.IsActive)).ToList();

            return new DashboardFilterOptionsDto(
                cashiers,
                waiters,
                categories,
                paymentMethods.Select(m => m.Value).ToList())
            {
                PaymentMethodOptions = paymentMethods,
                Partners = partners,
                Warehouses = warehouses
            };
        }

        private async Task<IReadOnlyList<PaymentMethodOptionDto>> GetPaymentMethodOptionsAsync(
            CancellationToken ct)
        {
            var branchId = _ctx.Filter.BranchId;
            var current = await _db.PaymentMethods.AsNoTracking()
                .OrderByDescending(m => m.IsActive)
                .ThenBy(m => m.DisplayOrder)
                .Select(m => new { m.Id, m.Code, m.NameEn, m.NameAr, m.IsActive, m.DisplayOrder })
                .ToListAsync(ct);

            var historyQuery = _db.Payments.AsNoTracking();
            if (branchId.HasValue)
                historyQuery = historyQuery.Where(p => p.BranchId == branchId.Value);

            var history = await historyQuery
                .Select(p => new
                {
                    p.PaymentMethodId,
                    p.PaymentMethodCode,
                    p.PaymentMethodName,
                    p.PaymentMethodNameAr,
                    p.Method
                })
                .Distinct()
                .ToListAsync(ct);

            var historicalIds = history
                .Where(p => p.PaymentMethodId.HasValue)
                .Select(p => p.PaymentMethodId!.Value)
                .Distinct()
                .ToList();
            var currentIds = current.Select(m => m.Id).ToHashSet();
            var deleted = await _db.PaymentMethods.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => historicalIds.Contains(m.Id) && !currentIds.Contains(m.Id))
                .Select(m => new { m.Id, m.Code, m.NameEn, m.NameAr })
                .ToListAsync(ct);

            var legacyOrderQuery = _db.Orders.AsNoTracking();
            if (branchId.HasValue)
                legacyOrderQuery = legacyOrderQuery.Where(o => o.BranchId == branchId.Value);

            var legacyOrderMethods = await legacyOrderQuery
                .Where(o => !o.Payments.Any() && !string.IsNullOrEmpty(o.PaymentMethod))
                .Select(o => o.PaymentMethod!)
                .Distinct()
                .ToListAsync(ct);

            var options = new Dictionary<string, PaymentMethodOptionDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var method in current)
                AddPaymentOption(options, method.Code, method.NameEn, method.NameAr, method.IsActive);
            foreach (var method in deleted)
                AddPaymentOption(options, $"PMID:{method.Id:N}", method.NameEn, method.NameAr, false);
            foreach (var payment in history)
                AddHistoricalPaymentOption(options, payment.PaymentMethodId, payment.PaymentMethodCode,
                    payment.PaymentMethodName, payment.PaymentMethodNameAr, payment.Method, currentIds);
            foreach (var method in legacyOrderMethods)
                AddPaymentOption(options, method, method, null, false);

            return options.Values
                .OrderByDescending(o => o.IsActive)
                .ThenBy(o => o.Name)
                .ToList();
        }

        private async Task<IReadOnlyList<PartnerOptionDto>> GetPartnerOptionsAsync(CancellationToken ct)
        {
            var branchId = _ctx.Filter.BranchId;
            var current = await _db.DeliveryPartners.AsNoTracking()
                .OrderBy(p => p.SortOrder)
                .Select(p => new { p.Id, p.Name, p.NameAr, p.Code, p.Status })
                .ToListAsync(ct);

            var historyQuery = _db.Orders.AsNoTracking();
            if (branchId.HasValue)
                historyQuery = historyQuery.Where(o => o.BranchId == branchId.Value);

            var history = await historyQuery
                .Where(o => o.OrderSource == OrderSource.Talabat
                    || o.OrderSource == OrderSource.DeliveryPartner
                    || o.DeliveryPartnerId.HasValue)
                .Select(o => new
                {
                    o.OrderSource,
                    o.DeliveryPartnerId,
                    o.DeliveryPartnerName,
                    o.DeliveryPartnerNameAr,
                    o.DeliveryPartnerCode
                })
                .Distinct()
                .ToListAsync(ct);

            var historicalIds = history
                .Where(o => o.DeliveryPartnerId.HasValue)
                .Select(o => o.DeliveryPartnerId!.Value)
                .Distinct()
                .ToList();
            var currentIds = current.Select(p => p.Id).ToHashSet();
            var deleted = await _db.DeliveryPartners.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => historicalIds.Contains(p.Id) && !currentIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.NameAr, p.Code })
                .ToListAsync(ct);

            var options = new Dictionary<string, PartnerOptionDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var partner in current)
                AddPartnerOption(options, $"ID:{partner.Id:N}", partner.Name, partner.NameAr,
                    partner.Status == DeliveryPartnerStatus.Active);
            foreach (var partner in deleted)
                AddPartnerOption(options, $"ID:{partner.Id:N}", partner.Name, partner.NameAr, false);
            var configuredIds = currentIds.Concat(deleted.Select(p => p.Id)).ToHashSet();
            foreach (var partner in history)
                AddHistoricalPartnerOption(options, partner.OrderSource, partner.DeliveryPartnerId,
                    partner.DeliveryPartnerCode, partner.DeliveryPartnerName, partner.DeliveryPartnerNameAr,
                    configuredIds);

            return options.Values
                .OrderByDescending(o => o.IsActive)
                .ThenBy(o => o.Name)
                .ToList();
        }

        private static void AddHistoricalPaymentOption(
            IDictionary<string, PaymentMethodOptionDto> options,
            Guid? id,
            string? code,
            string? name,
            string? nameAr,
            string? method,
            IReadOnlySet<Guid> currentIds)
        {
            if (id.HasValue && !currentIds.Contains(id.Value)) return;
            var value = FirstNotBlank(code, name, method);
            if (value is null) return;
            AddPaymentOption(options, value, FirstNotBlank(name, code, method)!, nameAr, false);
        }

        private static void AddPaymentOption(
            IDictionary<string, PaymentMethodOptionDto> options,
            string? value,
            string? name,
            string? nameAr,
            bool isActive)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(name)) return;
            var option = new PaymentMethodOptionDto(value.Trim(), name.Trim(), nameAr?.Trim(), isActive);
            var duplicate = options.FirstOrDefault(entry =>
                NormalizeOptionIdentity(entry.Value.Name) == NormalizeOptionIdentity(option.Name)
                || NormalizeOptionIdentity(entry.Value.Value) == NormalizeOptionIdentity(option.Value));

            if (string.IsNullOrEmpty(duplicate.Key))
            {
                options[option.Value] = option;
                return;
            }

            if (!duplicate.Value.IsActive && option.IsActive)
            {
                options.Remove(duplicate.Key);
                options[option.Value] = option;
            }
        }

        private static void AddHistoricalPartnerOption(
            IDictionary<string, PartnerOptionDto> options,
            OrderSource source,
            Guid? id,
            string? code,
            string? name,
            string? nameAr,
            IReadOnlySet<Guid> configuredIds)
        {
            if (source == OrderSource.Talabat)
            {
                AddPartnerOption(options, "TALABAT", "Talabat", "طلبات", true);
                return;
            }
            if (id.HasValue && configuredIds.Contains(id.Value)) return;
            var value = !string.IsNullOrWhiteSpace(code) ? $"CODE:{code.Trim()}"
                : !string.IsNullOrWhiteSpace(name) ? $"NAME:{name.Trim()}" : null;
            AddPartnerOption(options, value, FirstNotBlank(name, code), nameAr, false);
        }

        private static void AddPartnerOption(
            IDictionary<string, PartnerOptionDto> options,
            string? value,
            string? name,
            string? nameAr,
            bool isActive)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(name)) return;
            var option = new PartnerOptionDto(value.Trim(), name.Trim(), nameAr?.Trim(), isActive);
            if (!options.TryGetValue(option.Value, out var current) || (!current.IsActive && option.IsActive))
                options[option.Value] = option;
        }

        private static string? FirstNotBlank(params string?[] values)
            => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

        private static string NormalizeOptionIdentity(string value)
            => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();

        // Time-bucket TTL — past windows can cache aggressively (24h),
        // current/future windows use the per-scope live tier so the user
        // never sees stale numbers on "today".
        private TimeSpan TtlFor(DashboardScope scope)
        {
            var endIsHistorical = _ctx.Window.EndUtc <= DateTime.UtcNow.Date;
            if (endIsHistorical) return DashboardCacheTtl.Historical;

            return scope switch
            {
                DashboardScope.Operations => DashboardCacheTtl.Short,
                DashboardScope.Financial  => DashboardCacheTtl.Medium,
                DashboardScope.Inventory  => DashboardCacheTtl.Medium,
                DashboardScope.Audit      => DashboardCacheTtl.Long,
                _ => DashboardCacheTtl.Medium
            };
        }
    }
}
