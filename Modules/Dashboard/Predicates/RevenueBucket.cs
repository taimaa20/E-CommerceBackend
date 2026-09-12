namespace RestaurantPos.Api.Modules.Dashboard.Predicates
{
    /// <summary>
    /// Semantic classification of an order's contribution to revenue.
    /// The ONLY place these buckets are defined is
    /// <see cref="OrderLifecyclePredicates"/>.
    /// </summary>
    public enum RevenueBucket
    {
        /// <summary>Order is paid/served — counts toward confirmed revenue.</summary>
        Confirmed = 0,

        /// <summary>Order exists, not cancelled, not yet paid — pending revenue.</summary>
        Pending = 1,

        /// <summary>Order is cancelled — excluded from revenue, counted in cancellation analytics.</summary>
        Cancelled = 2,

        /// <summary>Order has a refund — negative revenue adjustment.</summary>
        Refunded = 3
    }
}
