using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Dashboard.Filters;
using RestaurantPos.Api.Options;

namespace RestaurantPos.Api.Modules.Dashboard.Queries
{
    /// <inheritdoc />
    /// <remarks>
    /// Multi-tenancy and soft-delete are already enforced by global query
    /// filters on <see cref="PosDbContext"/>. This builder only adds the
    /// dashboard-specific window + scope filters.
    /// </remarks>
    public sealed class MetricQueryBuilder : IMetricQueryBuilder
    {
        private readonly PosDbContext _db;
        private readonly IDashboardFilterContext _ctx;
        private readonly IOptionsMonitor<DashboardOptions> _options;

        public MetricQueryBuilder(
            PosDbContext db,
            IDashboardFilterContext ctx,
            IOptionsMonitor<DashboardOptions> options)
        {
            _db      = db      ?? throw new ArgumentNullException(nameof(db));
            _ctx     = ctx     ?? throw new ArgumentNullException(nameof(ctx));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IMetricQueryBuilder ForContext(IDashboardFilterContext context)
            => new MetricQueryBuilder(_db, context, _options);

        public bool ExcludesWasteLogs => ShouldExcludeWasteLogs();

        public IQueryable<Order> Orders()
        {
            var f = _ctx.Filter;
            var w = _ctx.Window;

            // Revenue-natural window: paid orders fall on their PaidAt date (when
            // money actually moved), unpaid orders fall on CreatedAt (so Active /
            // Pending widgets keep their semantics). EF translates the `??` to
            // COALESCE(paid_at, created_at), letting Postgres reuse a single index
            // on Orders.
            var q = _db.Orders.AsNoTracking()
                .Where(o => (o.PaidAt ?? o.CreatedAt) >= w.StartUtc && (o.PaidAt ?? o.CreatedAt) < w.EndUtc);

            return ApplyWasteLogOrderExclusion(ApplyOrderScopeFilters(q, f));
        }

        public IQueryable<Order> AllOrders()
        {
            var q = _db.Orders.AsNoTracking();
            if (_ctx.Filter.BranchId.HasValue) q = q.Where(o => o.BranchId == _ctx.Filter.BranchId.Value);
            return ApplyWasteLogOrderExclusion(q);
        }

        public IQueryable<OrderItem> OrderItems()
        {
            var f = _ctx.Filter;
            var w = _ctx.Window;

            // Same revenue-natural window as Orders() so item-level aggregates
            // (best sellers, menu engineering) reconcile with order-level ones.
            var q = _db.OrderItems.AsNoTracking()
                .Where(oi => (oi.Order.PaidAt ?? oi.Order.CreatedAt) >= w.StartUtc
                          && (oi.Order.PaidAt ?? oi.Order.CreatedAt) < w.EndUtc);

            q = ApplyOrderItemScopeFilters(q, f);

            return ApplyWasteLogOrderItemExclusion(q);
        }

        public IQueryable<Payment> Payments()
        {
            var w = _ctx.Window;
            var f = _ctx.Filter;

            // Payments are always tied to a paid order, so PaidAt is set; fall
            // back to CreatedAt for the (rare) partial-payment edge case.
            var q = _db.Payments.AsNoTracking()
                .Where(p => (p.Order.PaidAt ?? p.Order.CreatedAt) >= w.StartUtc
                         && (p.Order.PaidAt ?? p.Order.CreatedAt) < w.EndUtc);

            q = ApplyPaymentMethodFilter(q, f.PaymentMethod);
            if (f.BranchId.HasValue) q = q.Where(p => p.BranchId == f.BranchId.Value);
            if (f.CashierId.HasValue) q = q.Where(p => p.Order.PaidByUserId == f.CashierId);
            if (f.WaiterId.HasValue)  q = q.Where(p => p.Order.WaiterId == f.WaiterId);
            if (f.TableId.HasValue)   q = q.Where(p => p.Order.TableId == f.TableId);
            if (f.OrderType.HasValue) q = q.Where(p => p.Order.OrderType == f.OrderType);
            if (f.OrderSource.HasValue) q = q.Where(p => p.Order.OrderSource == f.OrderSource);
            if (f.Status.HasValue)    q = q.Where(p => p.Order.Status == f.Status);
            q = ApplyPartnerFilter(q, f.Partner);

            return ApplyWasteLogPaymentExclusion(q);
        }

        public IQueryable<WasteLog> WasteLogs()
        {
            var w = _ctx.Window;
            var f = _ctx.Filter;

            var q = _db.WasteLogs.AsNoTracking()
                .Where(wl => (wl.WasteDate ?? wl.CreatedAt) >= w.StartUtc
                          && (wl.WasteDate ?? wl.CreatedAt) < w.EndUtc);

            if (f.BranchId.HasValue) q = q.Where(wl => wl.BranchId == f.BranchId.Value);
            if (f.EmployeeId.HasValue) q = q.Where(wl => wl.LoggedById == f.EmployeeId);
            if (f.ProductId.HasValue)  q = q.Where(wl => wl.ProductId == f.ProductId || wl.ItemId == f.ProductId);

            return ShouldExcludeWasteLogs() ? q.Where(_ => false) : q;
        }

        public IQueryable<RefundLog> RefundLogs()
        {
            var w = _ctx.Window;
            var f = _ctx.Filter;

            var q = _db.RefundLogs.AsNoTracking()
                .Where(r => r.ProcessedAt >= w.StartUtc && r.ProcessedAt < w.EndUtc);

            if (f.BranchId.HasValue) q = q.Where(r => r.BranchId == f.BranchId.Value);
            if (f.EmployeeId.HasValue) q = q.Where(r => r.ProcessedById == f.EmployeeId);
            if (f.CashierId.HasValue) q = q.Where(r => r.Order.PaidByUserId == f.CashierId);
            if (f.TableId.HasValue)   q = q.Where(r => r.Order.TableId == f.TableId);
            if (f.OrderType.HasValue) q = q.Where(r => r.Order.OrderType == f.OrderType);
            if (f.OrderSource.HasValue) q = q.Where(r => r.Order.OrderSource == f.OrderSource);
            if (f.Status.HasValue)    q = q.Where(r => r.Order.Status == f.Status);
            q = ApplyPaymentMethodFilter(q, f.PaymentMethod);
            q = ApplyPartnerFilter(q, f.Partner);

            return ApplyWasteLogRefundExclusion(q);
        }

        public IQueryable<CancelLog> CancelLogs()
        {
            var w = _ctx.Window;
            var f = _ctx.Filter;

            var q = _db.CancelLogs.AsNoTracking()
                .Where(c => c.CancelledAt >= w.StartUtc && c.CancelledAt < w.EndUtc);

            if (f.BranchId.HasValue) q = q.Where(c => c.BranchId == f.BranchId.Value);
            if (f.EmployeeId.HasValue) q = q.Where(c => c.CancelledById == f.EmployeeId);
            if (f.CashierId.HasValue) q = q.Where(c => c.Order.PaidByUserId == f.CashierId);
            if (f.TableId.HasValue)   q = q.Where(c => c.Order.TableId == f.TableId);
            if (f.OrderType.HasValue) q = q.Where(c => c.Order.OrderType == f.OrderType);
            if (f.OrderSource.HasValue) q = q.Where(c => c.Order.OrderSource == f.OrderSource);
            if (f.Status.HasValue)    q = q.Where(c => c.Order.Status == f.Status);
            q = ApplyPaymentMethodFilter(q, f.PaymentMethod);
            q = ApplyPartnerFilter(q, f.Partner);

            return ApplyWasteLogCancelExclusion(q);
        }

        public IQueryable<CancelLog> AllCancelLogs()
        {
            var q = _db.CancelLogs.AsNoTracking();
            if (_ctx.Filter.BranchId.HasValue) q = q.Where(c => c.BranchId == _ctx.Filter.BranchId.Value);
            return ApplyWasteLogCancelExclusion(q);
        }

        public IQueryable<RawMaterial> RawMaterials()
            => _db.RawMaterials.AsNoTracking();

        public IQueryable<RawMaterialInventory> RawMaterialInventories()
        {
            var q = _db.RawMaterialInventories.AsNoTracking();
            var branchId = _ctx.Filter.BranchId;
            if (branchId.HasValue)
                q = q.Where(i => i.BranchId == branchId.Value);

            var warehouseId = _ctx.Filter.WarehouseId;
            if (warehouseId.HasValue)
                q = q.Where(i => i.WarehouseId == warehouseId.Value);
            return q;
        }

        public IQueryable<StockBatch> StockBatches()
        {
            var q = _db.StockBatches.AsNoTracking()
                .Where(b => b.RemainingQuantity > 0);
            var branchId = _ctx.Filter.BranchId;
            if (branchId.HasValue)
                q = q.Where(b => b.BranchId == branchId.Value);
            return q;
        }

        public IQueryable<TimeEntry> TimeEntries()
        {
            var w = _ctx.Window;
            return _db.TimeEntries.AsNoTracking()
                .Where(te => te.Date >= w.StartUtc && te.Date < w.EndUtc);
        }

        private IQueryable<Order> ApplyWasteLogOrderExclusion(IQueryable<Order> query)
            => ShouldExcludeWasteLogs()
                ? query.Where(o => !_db.WasteLogs.Any(w => w.OrderId == o.Id || w.SourceOrderId == o.Id)
                    && !_db.CancelLogs.Any(c => c.OrderId == o.Id && c.WasteLogId.HasValue))
                : query;

        private IQueryable<OrderItem> ApplyWasteLogOrderItemExclusion(IQueryable<OrderItem> query)
            => ShouldExcludeWasteLogs()
                ? query.Where(oi => !_db.WasteLogs.Any(w => w.SourceOrderItemId == oi.Id
                    || w.SourceOrderId == oi.OrderId
                    || w.OrderId == oi.OrderId)
                    && !_db.CancelLogs.Any(c => c.WasteLogId.HasValue
                        && (c.OrderItemId == oi.Id || c.OrderId == oi.OrderId)))
                : query;

        private IQueryable<Payment> ApplyWasteLogPaymentExclusion(IQueryable<Payment> query)
            => ShouldExcludeWasteLogs()
                ? query.Where(p => !_db.WasteLogs.Any(w => w.OrderId == p.OrderId || w.SourceOrderId == p.OrderId)
                    && !_db.CancelLogs.Any(c => c.OrderId == p.OrderId && c.WasteLogId.HasValue))
                : query;

        private IQueryable<RefundLog> ApplyWasteLogRefundExclusion(IQueryable<RefundLog> query)
            => ShouldExcludeWasteLogs()
                ? query.Where(r => !_db.WasteLogs.Any(w => w.OrderId == r.OrderId || w.SourceOrderId == r.OrderId)
                    && !_db.CancelLogs.Any(c => c.OrderId == r.OrderId && c.WasteLogId.HasValue))
                : query;

        private IQueryable<CancelLog> ApplyWasteLogCancelExclusion(IQueryable<CancelLog> query)
            => ShouldExcludeWasteLogs()
                ? query.Where(c => !c.WasteLogId.HasValue
                    && !_db.WasteLogs.Any(w => w.OrderId == c.OrderId || w.SourceOrderId == c.OrderId))
                : query;

        private bool ShouldExcludeWasteLogs()
            => _options.CurrentValue.ShouldExcludeWasteLogs(_ctx.Scope);

        private static IQueryable<Order> ApplyOrderScopeFilters(IQueryable<Order> q, DashboardFilterDto f)
        {
            if (f.BranchId.HasValue) q = q.Where(o => o.BranchId == f.BranchId.Value);
            if (f.CashierId.HasValue) q = q.Where(o => o.PaidByUserId == f.CashierId);
            if (f.WaiterId.HasValue)  q = q.Where(o => o.WaiterId == f.WaiterId);
            if (f.TableId.HasValue)   q = q.Where(o => o.TableId == f.TableId);
            if (f.OrderType.HasValue) q = q.Where(o => o.OrderType == f.OrderType);
            if (f.OrderSource.HasValue) q = q.Where(o => o.OrderSource == f.OrderSource);
            if (f.Status.HasValue)    q = q.Where(o => o.Status == f.Status);
            q = ApplyPaymentMethodFilter(q, f.PaymentMethod);
            q = ApplyPartnerFilter(q, f.Partner);
            return q;
        }

        private static IQueryable<OrderItem> ApplyOrderItemScopeFilters(IQueryable<OrderItem> q, DashboardFilterDto f)
        {
            if (f.BranchId.HasValue) q = q.Where(oi => oi.BranchId == f.BranchId.Value);
            if (f.ProductId.HasValue)  q = q.Where(oi => oi.ProductId == f.ProductId);
            if (f.CategoryId.HasValue) q = q.Where(oi => oi.Product.CategoryId == f.CategoryId);
            if (f.CashierId.HasValue)  q = q.Where(oi => oi.Order.PaidByUserId == f.CashierId);
            if (f.WaiterId.HasValue)   q = q.Where(oi => oi.Order.WaiterId == f.WaiterId);
            if (f.TableId.HasValue)    q = q.Where(oi => oi.Order.TableId == f.TableId);
            if (f.OrderType.HasValue)  q = q.Where(oi => oi.Order.OrderType == f.OrderType);
            if (f.OrderSource.HasValue) q = q.Where(oi => oi.Order.OrderSource == f.OrderSource);
            if (f.Status.HasValue)     q = q.Where(oi => oi.Order.Status == f.Status);
            q = ApplyPaymentMethodFilter(q, f.PaymentMethod);
            q = ApplyPartnerFilter(q, f.Partner);
            return q;
        }

        private static IQueryable<Payment> ApplyPaymentMethodFilter(
            IQueryable<Payment> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (TryParseKeyGuid(value, "PMID:", out var id))
                return query.Where(p => p.PaymentMethodId == id);

            var normalized = value.Trim().ToUpperInvariant();
            return query.Where(p => (p.Method != null && p.Method.ToUpper() == normalized)
                || (p.PaymentMethodCode != null && p.PaymentMethodCode.ToUpper() == normalized)
                || (p.PaymentMethodName != null && p.PaymentMethodName.ToUpper() == normalized)
                || (p.PaymentMethod != null
                    && (p.PaymentMethod.Code.ToUpper() == normalized
                        || p.PaymentMethod.NameEn.ToUpper() == normalized)));
        }

        private static IQueryable<Order> ApplyPaymentMethodFilter(
            IQueryable<Order> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (TryParseKeyGuid(value, "PMID:", out var id))
                return query.Where(o => o.Payments.Any(p => p.PaymentMethodId == id));

            var normalized = value.Trim().ToUpperInvariant();
            return query.Where(o => (o.PaymentMethod != null && o.PaymentMethod.ToUpper() == normalized)
                || o.Payments.Any(p => (p.Method != null && p.Method.ToUpper() == normalized)
                    || (p.PaymentMethodCode != null && p.PaymentMethodCode.ToUpper() == normalized)
                    || (p.PaymentMethodName != null && p.PaymentMethodName.ToUpper() == normalized)
                    || (p.PaymentMethod != null
                        && (p.PaymentMethod.Code.ToUpper() == normalized
                            || p.PaymentMethod.NameEn.ToUpper() == normalized))));
        }

        private static IQueryable<OrderItem> ApplyPaymentMethodFilter(
            IQueryable<OrderItem> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (TryParseKeyGuid(value, "PMID:", out var id))
                return query.Where(i => i.Order.Payments.Any(p => p.PaymentMethodId == id));

            var normalized = value.Trim().ToUpperInvariant();
            return query.Where(i => (i.Order.PaymentMethod != null
                    && i.Order.PaymentMethod.ToUpper() == normalized)
                || i.Order.Payments.Any(p => (p.Method != null && p.Method.ToUpper() == normalized)
                    || (p.PaymentMethodCode != null && p.PaymentMethodCode.ToUpper() == normalized)
                    || (p.PaymentMethodName != null && p.PaymentMethodName.ToUpper() == normalized)
                    || (p.PaymentMethod != null
                        && (p.PaymentMethod.Code.ToUpper() == normalized
                            || p.PaymentMethod.NameEn.ToUpper() == normalized))));
        }

        private static IQueryable<RefundLog> ApplyPaymentMethodFilter(
            IQueryable<RefundLog> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (TryParseKeyGuid(value, "PMID:", out var id))
                return query.Where(r => r.Order.Payments.Any(p => p.PaymentMethodId == id));

            var normalized = value.Trim().ToUpperInvariant();
            return query.Where(r => (r.OriginalPaymentMethod != null
                    && r.OriginalPaymentMethod.ToUpper() == normalized)
                || (r.Order.PaymentMethod != null && r.Order.PaymentMethod.ToUpper() == normalized)
                || r.Order.Payments.Any(p => (p.Method != null && p.Method.ToUpper() == normalized)
                    || (p.PaymentMethodCode != null && p.PaymentMethodCode.ToUpper() == normalized)
                    || (p.PaymentMethodName != null && p.PaymentMethodName.ToUpper() == normalized)
                    || (p.PaymentMethod != null
                        && (p.PaymentMethod.Code.ToUpper() == normalized
                            || p.PaymentMethod.NameEn.ToUpper() == normalized))));
        }

        private static IQueryable<CancelLog> ApplyPaymentMethodFilter(
            IQueryable<CancelLog> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (TryParseKeyGuid(value, "PMID:", out var id))
                return query.Where(c => c.Order.Payments.Any(p => p.PaymentMethodId == id));

            var normalized = value.Trim().ToUpperInvariant();
            return query.Where(c => (c.Order.PaymentMethod != null
                    && c.Order.PaymentMethod.ToUpper() == normalized)
                || c.Order.Payments.Any(p => (p.Method != null && p.Method.ToUpper() == normalized)
                    || (p.PaymentMethodCode != null && p.PaymentMethodCode.ToUpper() == normalized)
                    || (p.PaymentMethodName != null && p.PaymentMethodName.ToUpper() == normalized)
                    || (p.PaymentMethod != null
                        && (p.PaymentMethod.Code.ToUpper() == normalized
                            || p.PaymentMethod.NameEn.ToUpper() == normalized))));
        }

        private static IQueryable<Order> ApplyPartnerFilter(
            IQueryable<Order> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (string.Equals(value, "TALABAT", StringComparison.OrdinalIgnoreCase))
                return query.Where(o => o.OrderSource == OrderSource.Talabat);
            if (TryParseKeyGuid(value, "ID:", out var id))
                return query.Where(o => o.DeliveryPartnerId == id);
            if (value.StartsWith("CODE:", StringComparison.OrdinalIgnoreCase))
            {
                var code = value.Substring(5);
                return query.Where(o => o.DeliveryPartnerCode == code);
            }
            if (value.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase))
            {
                var name = value.Substring(5);
                return query.Where(o => o.DeliveryPartnerName == name);
            }
            return query;
        }

        private static IQueryable<OrderItem> ApplyPartnerFilter(
            IQueryable<OrderItem> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (string.Equals(value, "TALABAT", StringComparison.OrdinalIgnoreCase))
                return query.Where(i => i.Order.OrderSource == OrderSource.Talabat);
            if (TryParseKeyGuid(value, "ID:", out var id))
                return query.Where(i => i.Order.DeliveryPartnerId == id);
            if (value.StartsWith("CODE:", StringComparison.OrdinalIgnoreCase))
            {
                var code = value.Substring(5);
                return query.Where(i => i.Order.DeliveryPartnerCode == code);
            }
            if (value.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase))
            {
                var name = value.Substring(5);
                return query.Where(i => i.Order.DeliveryPartnerName == name);
            }
            return query;
        }

        private static IQueryable<Payment> ApplyPartnerFilter(
            IQueryable<Payment> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (string.Equals(value, "TALABAT", StringComparison.OrdinalIgnoreCase))
                return query.Where(p => p.Order.OrderSource == OrderSource.Talabat);
            if (TryParseKeyGuid(value, "ID:", out var id))
                return query.Where(p => p.Order.DeliveryPartnerId == id);
            if (value.StartsWith("CODE:", StringComparison.OrdinalIgnoreCase))
            {
                var code = value.Substring(5);
                return query.Where(p => p.Order.DeliveryPartnerCode == code);
            }
            if (value.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase))
            {
                var name = value.Substring(5);
                return query.Where(p => p.Order.DeliveryPartnerName == name);
            }
            return query;
        }

        private static IQueryable<RefundLog> ApplyPartnerFilter(
            IQueryable<RefundLog> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (string.Equals(value, "TALABAT", StringComparison.OrdinalIgnoreCase))
                return query.Where(r => r.Order.OrderSource == OrderSource.Talabat);
            if (TryParseKeyGuid(value, "ID:", out var id))
                return query.Where(r => r.Order.DeliveryPartnerId == id);
            if (value.StartsWith("CODE:", StringComparison.OrdinalIgnoreCase))
            {
                var code = value.Substring(5);
                return query.Where(r => r.Order.DeliveryPartnerCode == code);
            }
            if (value.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase))
            {
                var name = value.Substring(5);
                return query.Where(r => r.Order.DeliveryPartnerName == name);
            }
            return query;
        }

        private static IQueryable<CancelLog> ApplyPartnerFilter(
            IQueryable<CancelLog> query,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return query;
            if (string.Equals(value, "TALABAT", StringComparison.OrdinalIgnoreCase))
                return query.Where(c => c.Order.OrderSource == OrderSource.Talabat);
            if (TryParseKeyGuid(value, "ID:", out var id))
                return query.Where(c => c.Order.DeliveryPartnerId == id);
            if (value.StartsWith("CODE:", StringComparison.OrdinalIgnoreCase))
            {
                var code = value.Substring(5);
                return query.Where(c => c.Order.DeliveryPartnerCode == code);
            }
            if (value.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase))
            {
                var name = value.Substring(5);
                return query.Where(c => c.Order.DeliveryPartnerName == name);
            }
            return query;
        }

        private static bool TryParseKeyGuid(string value, string prefix, out Guid id)
        {
            id = Guid.Empty;
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(value[prefix.Length..], out id);
        }
    }
}
