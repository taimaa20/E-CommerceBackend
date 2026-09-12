namespace RestaurantPos.Api.Helpers
{
    /// <summary>
    /// THE single source of truth for inventory stock-level states. Every module
    /// — dashboard KPIs, the warehouse dashboard, inventory reports, a future
    /// branch dashboard, low-stock notifications and automatic replenishment —
    /// must classify stock through this helper so the business never ends up with
    /// inconsistent definitions.
    /// </summary>
    /// <remarks>
    /// The three states intentionally OVERLAP where the business meaning overlaps
    /// (an out-of-stock item is also below its reorder level). Callers decide how
    /// to present them; the policy only answers each question independently.
    ///
    /// Thresholds are passed in so the same logic serves both the per-warehouse
    /// ledger (<c>RawMaterialInventory.MinimumQuantity</c> / <c>ReorderLevel</c>)
    /// and the aggregate material (<c>RawMaterial.MinimumAlertLevel</c> used for
    /// both thresholds, preserving the existing all-warehouses behaviour).
    /// </remarks>
    public static class StockLevelPolicy
    {
        /// <summary>Nothing on hand (0 or, defensively, below).</summary>
        public static bool IsOutOfStock(decimal quantity) => quantity <= 0m;

        /// <summary>Still available but at or under the critical minimum.</summary>
        public static bool IsLowStock(decimal quantity, decimal minimumQuantity)
            => quantity > 0m && quantity <= minimumQuantity;

        /// <summary>At or under the reorder (procurement-planning) level — overlaps Out and Low.</summary>
        public static bool IsReorderRequired(decimal quantity, decimal reorderLevel)
            => quantity <= reorderLevel;
    }
}
