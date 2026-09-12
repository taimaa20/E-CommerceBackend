namespace RestaurantPos.Api.Modules.Dashboard.Services.Realtime
{
    /// <summary>
    /// SignalR event names emitted on <see cref="Hubs.DashboardHub"/>.
    /// Always use these constants — never inline strings (CLAUDE.md rule).
    /// </summary>
    public static class DashboardEventNames
    {
        /// <summary>Operations snapshot has changed — clients should refetch the operations scope.</summary>
        public const string OperationsInvalidated = "OperationsInvalidated";

        /// <summary>Financial snapshot has changed.</summary>
        public const string FinancialInvalidated  = "FinancialInvalidated";

        /// <summary>Inventory snapshot has changed.</summary>
        public const string InventoryInvalidated  = "InventoryInvalidated";

        /// <summary>Audit snapshot has changed.</summary>
        public const string AuditInvalidated      = "AuditInvalidated";

        /// <summary>A toast-style alert (low stock, printer offline, spike).</summary>
        public const string AlertRaised           = "DashboardAlertRaised";
    }
}
