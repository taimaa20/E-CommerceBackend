using System.Linq.Expressions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    /// <summary>
    /// Single source of truth for "is this order completed?". Completion is
    /// derived from the independent payment, readiness, and handoff signals.
    ///
    /// Cancelled orders are NOT completed but also NOT open — callers handle
    /// them via <see cref="IsClosed"/> when they want "doesn't block close".
    /// </summary>
    public static class OrderCompletion
    {
        /// <summary>True when the order is in its successful terminal state.</summary>
        public static bool IsCompleted(Order o)
        {
            if (o is null) return false;
            if (o.Status == OrderStatus.Cancelled) return false;
            if (o.Status == OrderStatus.Completed) return true;
            return IsPaid(o) && IsReady(o) && IsHandedOff(o);
        }

        public static void ApplyStatus(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed)
                return;

            StampReadyAtIfNeeded(order);

            if (IsPaid(order) && IsReady(order) && IsHandedOff(order))
            {
                order.Status = OrderStatus.Completed;
                return;
            }

            if (IsHandedOff(order))
                order.Status = OrderStatus.Served;
            else if (IsPaid(order))
                order.Status = OrderStatus.Paid;
            else if (IsReady(order))
                order.Status = OrderStatus.Ready;
        }

        /// <summary>
        /// True when the order no longer blocks shift close — either completed
        /// successfully or cancelled. Mirrors <see cref="IsCompleted"/> plus the
        /// Cancelled state.
        /// </summary>
        public static bool IsClosed(Order o)
            => o is not null && (o.Status == OrderStatus.Cancelled || IsCompleted(o));

        /// <summary>True when the order is still open and would block close.</summary>
        public static bool IsOpen(Order o) => !IsClosed(o);

        /// <summary>
        /// EF Core expression mirror of <see cref="IsCompleted"/> — pass into
        /// <c>.Where()</c>/<c>.CountAsync()</c> so the predicate translates to SQL
        /// instead of pulling rows into memory. MUST stay byte-identical to the
        /// in-memory version above.
        /// </summary>
        public static Expression<System.Func<Order, bool>> IsCompletedExpr =>
            o => o.Status != OrderStatus.Cancelled
              && (o.Status == OrderStatus.Completed
               || (o.Status == OrderStatus.Served
                && !o.OrderItems.Any(i => !i.IsReady)
                && (o.OrderSource == OrderSource.Online
                    ? o.PaidAt.HasValue
                      || o.Status == OrderStatus.Paid
                      || (o.Payments.Any()
                       && (o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= o.TotalAmount)
                    : o.PaidAt.HasValue
                      || (o.PaymentMethod != null && o.PaymentMethod != string.Empty)
                      || (o.Payments.Any()
                       && (o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= o.TotalAmount))));

        /// <summary>EF Core expression mirror of <see cref="IsClosed"/>.</summary>
        public static Expression<System.Func<Order, bool>> IsClosedExpr =>
            o => o.Status == OrderStatus.Cancelled
              || o.Status == OrderStatus.Completed
              || (o.Status == OrderStatus.Served
               && !o.OrderItems.Any(i => !i.IsReady)
               && (o.OrderSource == OrderSource.Online
                   ? o.PaidAt.HasValue
                     || o.Status == OrderStatus.Paid
                     || (o.Payments.Any()
                      && (o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= o.TotalAmount)
                   : o.PaidAt.HasValue
                     || (o.PaymentMethod != null && o.PaymentMethod != string.Empty)
                     || (o.Payments.Any()
                      && (o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= o.TotalAmount)));

        /// <summary>EF Core expression mirror of <see cref="IsOpen"/>.</summary>
        public static Expression<System.Func<Order, bool>> IsOpenExpr =>
            o => o.Status != OrderStatus.Cancelled
              && o.Status != OrderStatus.Completed
              && (o.Status != OrderStatus.Served
               || o.OrderItems.Any(i => !i.IsReady)
               || (o.OrderSource == OrderSource.Online
                   ? !o.PaidAt.HasValue
                     && o.Status != OrderStatus.Paid
                     && (!o.Payments.Any()
                      || (o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) < o.TotalAmount)
                   : !o.PaidAt.HasValue
                     && (o.PaymentMethod == null || o.PaymentMethod == string.Empty)
                     && (!o.Payments.Any()
                      || (o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) < o.TotalAmount)));

        /// <summary>
        /// Stamps <see cref="Order.ReadyAt"/> the first time every item on the order
        /// is ready (the canonical kitchen-prep-complete moment). Never overwrites an
        /// existing value and never stamps an order with no items, so a brand-new
        /// empty order can't be falsely marked ready.
        /// </summary>
        public static void StampReadyAtIfNeeded(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);
            if (order.ReadyAt is not null) return;
            if (order.OrderItems is null || order.OrderItems.Count == 0) return;
            if (order.OrderItems.Any(i => !i.IsReady)) return;
            order.ReadyAt = DateTime.UtcNow;
        }

        private static bool IsReady(Order order)
            => order.OrderItems == null || !order.OrderItems.Any(i => !i.IsReady);

        private static bool IsPaid(Order order)
            => OrderPaymentHelper.BuildSnapshot(order).IsPaid;

        private static bool IsHandedOff(Order order)
            => order.Status is OrderStatus.Served or OrderStatus.Completed;
    }
}
