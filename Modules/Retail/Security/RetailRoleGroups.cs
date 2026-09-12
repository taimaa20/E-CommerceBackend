using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Retail.Security
{
    /// <summary>
    /// Named authorization groups for the Retail module. Values alias existing
    /// <see cref="AppRoleGroups"/> constants today; naming them here means a future change
    /// ("let a merchandiser view the catalogue but not costs") touches one file.
    /// Never inline a role string in a Retail controller.
    /// </summary>
    public static class RetailRoleGroups
    {
        public const string RetailCatalogViewers = AppRoleGroups.AdminOnly;

        /// <summary>Read the finished-goods ledger and purchasing documents.</summary>
        public const string RetailStockViewers = AppRoleGroups.AdminOnly;

        /// <summary>Raise, edit and receive retail purchase orders. Receiving moves stock and
        /// changes product cost, so it stays with the same people who run procurement.</summary>
        public const string RetailPurchasingOperators = AppRoleGroups.AdminOnly;

        /// <summary>
        /// Manually correct finished-goods stock. Deliberately the same grant as the
        /// raw-material equivalent (<c>StockAdjustmentsController</c>, SuperAdmin only) so that
        /// "adjust stock" means the same thing in the one Inventory area whichever inventory
        /// type the operator is looking at.
        /// </summary>
        public const string RetailStockAdjusters = AppRoleNames.SuperAdmin;
    }
}
