using System.Linq.Expressions;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Modules.Dashboard.Predicates
{
    /// <summary>
    /// THE single source of truth for "what does this order mean for analytics?".
    /// Every dashboard calculator composes these predicates. No other class in
    /// the Dashboard module is permitted to write <c>o.Status == ...</c> directly.
    /// </summary>
    /// <remarks>
    /// The "Confirmed revenue" rule deliberately delegates to the legacy
    /// <see cref="OrderPaymentQuery.IsPaidExpression"/> so the new dashboards
    /// produce numerically identical totals to the existing Z-report and
    /// profit-loss screens on day one.
    /// </remarks>
    public static class OrderLifecyclePredicates
    {
        // ─── Revenue buckets ────────────────────────────────────────────────

        public static Expression<Func<Order, bool>> Confirmed => OrderPaymentQuery.IsPaidExpression;

        /// <summary>
        /// Keeps paid sales visible after a refund changes the order status to
        /// Cancelled. Refund amounts are recognized separately by ProcessedAt.
        /// </summary>
        public static Expression<Func<Order, bool>> RecordedSale =>
            o => o.PaidAt.HasValue
                || o.Status == OrderStatus.Paid
                || o.Status == OrderStatus.Completed
                || (o.Status != OrderStatus.Cancelled
                    && o.PaymentMethod != null
                    && o.PaymentMethod != string.Empty)
                || (o.Payments.Any()
                    && (o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= o.TotalAmount);

        public static Expression<Func<Order, bool>> Pending =>
            o => o.Status != OrderStatus.Cancelled
                && !o.PaidAt.HasValue
                && o.Status != OrderStatus.Paid
                && o.Status != OrderStatus.Completed
                && (o.PaymentMethod == null || o.PaymentMethod == string.Empty);

        public static Expression<Func<Order, bool>> Cancelled =>
            o => o.Status == OrderStatus.Cancelled;

        // ─── Lifecycle slices (operations dashboard) ────────────────────────

        /// <summary>Order is past creation but not yet finished.</summary>
        public static Expression<Func<Order, bool>> Active =>
            o => o.Status == OrderStatus.New
              || o.Status == OrderStatus.Preparing
              || o.Status == OrderStatus.Ready;

        public static Expression<Func<Order, bool>> Waiting   => o => o.Status == OrderStatus.New;
        public static Expression<Func<Order, bool>> Preparing => o => o.Status == OrderStatus.Preparing;
        public static Expression<Func<Order, bool>> Ready     => o => o.Status == OrderStatus.Ready;
        public static Expression<Func<Order, bool>> Served    => o => o.Status == OrderStatus.Served;

        // ─── Helpers usable from LINQ-to-Entities (translatable) ────────────

        /// <summary>An order is "stale" when it has been Preparing or Ready longer than the threshold.</summary>
        public static Expression<Func<Order, bool>> StaleSince(DateTime utcThreshold) =>
            o => (o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready)
              && o.CreatedAt < utcThreshold;

        /// <summary>
        /// OrderItem-side equivalent of <see cref="Confirmed"/>. Mirrors the legacy
        /// <see cref="OrderPaymentQuery.WherePaidOrder()"/> rule so revenue derived
        /// from OrderItems matches revenue derived from Orders to the cent.
        /// </summary>
        public static Expression<Func<OrderItem, bool>> ConfirmedItem =>
            oi => oi.Order.Status != OrderStatus.Cancelled
               && (oi.Order.PaidAt.HasValue
                   || oi.Order.Status == OrderStatus.Paid
                   || oi.Order.Status == OrderStatus.Completed
                   || (oi.Order.PaymentMethod != null && oi.Order.PaymentMethod != string.Empty)
                   || (oi.Order.Payments.Any() && (oi.Order.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= oi.Order.TotalAmount));

        public static Expression<Func<OrderItem, bool>> RecordedSaleItem =>
            oi => oi.Order.PaidAt.HasValue
                || oi.Order.Status == OrderStatus.Paid
                || oi.Order.Status == OrderStatus.Completed
                || (oi.Order.Status != OrderStatus.Cancelled
                    && oi.Order.PaymentMethod != null
                    && oi.Order.PaymentMethod != string.Empty)
                || (oi.Order.Payments.Any()
                    && (oi.Order.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= oi.Order.TotalAmount);

        public static Expression<Func<Payment, bool>> ConfirmedPayment =>
            p => p.Order.Status != OrderStatus.Cancelled
               && (p.Order.PaidAt.HasValue
                   || p.Order.Status == OrderStatus.Paid
                   || p.Order.Status == OrderStatus.Completed
                   || (p.Order.PaymentMethod != null && p.Order.PaymentMethod != string.Empty)
                   || (p.Order.Payments.Sum(x => (decimal?)x.Amount) ?? 0m) >= p.Order.TotalAmount);

        public static Expression<Func<Payment, bool>> RecordedSalePayment =>
            p => p.Order.PaidAt.HasValue
                || p.Order.Status == OrderStatus.Paid
                || p.Order.Status == OrderStatus.Completed
                || (p.Order.Status != OrderStatus.Cancelled
                    && p.Order.PaymentMethod != null
                    && p.Order.PaymentMethod != string.Empty)
                || (p.Order.Payments.Sum(x => (decimal?)x.Amount) ?? 0m) >= p.Order.TotalAmount;

        public static Expression<Func<Order, bool>> InBucket(RevenueBucket bucket) => bucket switch
        {
            RevenueBucket.Confirmed => Confirmed,
            RevenueBucket.Pending   => Pending,
            RevenueBucket.Cancelled => Cancelled,
            // Refunded resolved via RefundLog join — caller must use that path.
            RevenueBucket.Refunded  => o => false,
            _                       => o => true
        };
    }
}
