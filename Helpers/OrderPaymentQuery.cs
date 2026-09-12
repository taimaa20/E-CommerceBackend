using System.Linq.Expressions;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers
{
    public static class OrderPaymentQuery
    {
        public static Expression<Func<Order, bool>> IsPaidExpression => o =>
            o.Status != OrderStatus.Cancelled &&
            (
                o.PaidAt.HasValue ||
                o.Status == OrderStatus.Paid ||
                o.Status == OrderStatus.Completed ||
                (o.PaymentMethod != null && o.PaymentMethod != string.Empty) ||
                (o.Payments.Any() && (o.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= o.TotalAmount)
            );

        public static IQueryable<Order> WherePaid(this IQueryable<Order> query)
            => query.Where(IsPaidExpression);

        public static IQueryable<OrderItem> WherePaidOrder(this IQueryable<OrderItem> query)
            => query.Where(oi =>
                oi.Order.Status != OrderStatus.Cancelled &&
                (
                    oi.Order.PaidAt.HasValue ||
                    oi.Order.Status == OrderStatus.Paid ||
                    oi.Order.Status == OrderStatus.Completed ||
                    (oi.Order.PaymentMethod != null && oi.Order.PaymentMethod != string.Empty) ||
                    (oi.Order.Payments.Any() && (oi.Order.Payments.Sum(p => (decimal?)p.Amount) ?? 0m) >= oi.Order.TotalAmount)
                ));

        public static IQueryable<Order> WhereActiveLifecycle(this IQueryable<Order> query)
            => query.Where(o => o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready);

        public static IQueryable<Order> WhereCompletedLifecycle(this IQueryable<Order> query)
            => query.Where(OrderCompletion.IsCompletedExpr);
    }
}
