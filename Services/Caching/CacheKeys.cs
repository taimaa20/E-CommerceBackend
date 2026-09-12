using System;

namespace RestaurantPos.Api.Services.Caching
{
    /// <summary>
    /// Centralized cache key management for consistent naming and invalidation.
    /// Thread-safe and immutable key generation.
    /// </summary>
    public static class CacheKeys
    {
        private const string Prefix = "pos";
        private const string Separator = ":";

        #region Products

        /// <summary>
        /// Cache key for all products in a tenant (used by GET /api/Products).
        /// Keyed per-locale because the projected DTO bakes in DisplayName /
        /// CategoryDisplayName at query time — a warmed EN cache must not
        /// serve an AR request, and vice versa.
        /// </summary>
        public static string ProductsAll(Guid tenantId, bool isArabic) =>
            $"{Prefix}{Separator}products{Separator}all{Separator}{(isArabic ? "ar" : "en")}{Separator}{tenantId}";

        /// <summary>
        /// Cache key for active products only. Keyed per-locale for the same
        /// reason as <see cref="ProductsAll"/>.
        /// </summary>
        public static string ProductsActive(Guid tenantId, bool isArabic) =>
            $"{Prefix}{Separator}products{Separator}active{Separator}{(isArabic ? "ar" : "en")}{Separator}{tenantId}";

        public static string ProductsCashier(Guid tenantId, bool isArabic, Guid? deliveryPartnerId) =>
            $"{Prefix}{Separator}products{Separator}cashier{Separator}{(isArabic ? "ar" : "en")}{Separator}{(deliveryPartnerId?.ToString() ?? "pos")}{Separator}{tenantId}";

        /// <summary>
        /// Cache key for the public (lean) active-products projection.
        /// Keyed per-locale because DisplayName + CategoryDisplayName are baked in
        /// at projection time — a warmed EN cache must not serve an AR request.
        /// </summary>
        public static string ProductsPublic(Guid tenantId, bool isArabic) =>
            $"{Prefix}{Separator}products{Separator}public{Separator}{(isArabic ? "ar" : "en")}{Separator}{tenantId}";

        /// <summary>Cache key for a single product (optional optimization)</summary>
        public static string Product(Guid id) => 
            $"{Prefix}{Separator}product{Separator}{id}";

        /// <summary>Pattern to invalidate all product-related caches for a tenant</summary>
        public static string ProductsPattern(Guid tenantId) => 
            $"{Prefix}{Separator}products{Separator}*{Separator}{tenantId}";

        #endregion

        #region Categories

        /// <summary>Cache key for all categories (frequently accessed with products)</summary>
        public static string CategoriesAll(Guid tenantId) => 
            $"{Prefix}{Separator}categories{Separator}all{Separator}{tenantId}";

        #endregion

        #region Settings

        /// <summary>Cache key for system settings</summary>
        public static string SettingsAll(Guid? tenantId = null) => 
            tenantId.HasValue 
                ? $"{Prefix}{Separator}settings{Separator}all{Separator}{tenantId.Value}"
                : $"{Prefix}{Separator}settings{Separator}all{Separator}global";

        #endregion

        #region Payment Methods

        public static string PaymentMethodsActive(Guid tenantId) =>
            $"{Prefix}{Separator}payment-methods{Separator}active{Separator}{tenantId}";

        public static string PaymentMethodsAll(Guid tenantId) =>
            $"{Prefix}{Separator}payment-methods{Separator}all{Separator}{tenantId}";

        #endregion

        #region Orders

        /// <summary>Cache key for cashier orders list – today filter (15 s absolute TTL)</summary>
        public static string OrdersCashierToday(Guid tenantId) =>
            $"{Prefix}{Separator}orders{Separator}cashier{Separator}today{Separator}{tenantId}";

        /// <summary>Cache key for cashier orders list – all filter (20 s absolute TTL)</summary>
        public static string OrdersCashierAll(Guid tenantId) =>
            $"{Prefix}{Separator}orders{Separator}cashier{Separator}all{Separator}{tenantId}";

        /// <summary>
        /// Cache key for the takeaway active orders board (GET /orders/takeaway/active).
        /// TTL: 8 s absolute — short enough to stay accurate, long enough to absorb burst polling.
        /// Must be invalidated on any mutation that affects active takeaway orders.
        /// </summary>
        public static string TakeawayActiveOrders(Guid tenantId, Guid branchId) =>
            $"{Prefix}{Separator}orders{Separator}takeaway{Separator}active{Separator}{tenantId}{Separator}{branchId:N}";

        public static string TakeawayActiveOrdersPattern(Guid tenantId) =>
            $"{Prefix}{Separator}orders{Separator}takeaway{Separator}active{Separator}{tenantId}{Separator}*";

        /// <summary>
        /// Cache key for the KDS kitchen screen — today filter (today's orders + unfinished previous-day orders).
        /// Keyed by language because TableName is locale-dependent.
        /// TTL: 15 s absolute.
        /// </summary>
        public static string KitchenToday(bool isArabic = false) =>
            $"{Prefix}{Separator}orders{Separator}kitchen{Separator}today{Separator}{(isArabic ? "ar" : "en")}";

        /// <summary>
        /// Cache key for the KDS kitchen screen — all filter (last 3 days, no smart date cut-off).
        /// Keyed by language because TableName is locale-dependent.
        /// TTL: 15 s absolute.
        /// </summary>
        public static string KitchenAll(bool isArabic = false) =>
            $"{Prefix}{Separator}orders{Separator}kitchen{Separator}all{Separator}{(isArabic ? "ar" : "en")}";

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generate a hash-based cache key for paginated/filtered queries.
        /// Use this when caching specific filter combinations.
        /// </summary>
        public static string GenerateFilteredKey(string prefix, Guid tenantId, params object[] parameters)
        {
            var paramHash = string.Join("_", parameters).GetHashCode();
            return $"{Prefix}{Separator}{prefix}{Separator}filtered{Separator}{tenantId}{Separator}{paramHash}";
        }

        #endregion
    }
}
