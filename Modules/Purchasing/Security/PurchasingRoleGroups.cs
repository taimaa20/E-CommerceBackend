using System.Security.Claims;
using RestaurantPos.Api.Modules.Retail.Security;
using RestaurantPos.Api.Security;

namespace RestaurantPos.Api.Modules.Purchasing.Security
{
    /// <summary>
    /// Authorization for the one Purchasing screen.
    ///
    /// The screen is unified but the two grants behind it are not: raw-material procurement is
    /// Admin + Kitchen, finished-goods purchasing is Admin + Manager. Merging the list must not
    /// widen either one, so the door is the union and the rows are filtered per caller.
    /// </summary>
    public static class PurchasingRoleGroups
    {
        /// <summary>Union of the two existing purchasing grants — who may open the screen.</summary>
        public const string PurchaseOrderViewers =
            AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Kitchen;
    }

    /// <summary>Which halves of the merged purchase-order list this caller may see.</summary>
    public readonly record struct PurchasingVisibility(
        bool IncludeRawMaterials,
        bool IncludeFinishedProducts)
    {
        /// <summary>
        /// Derived from the existing grant constants rather than restated, so a future change to
        /// either grant is picked up here without editing this file.
        /// </summary>
        public static PurchasingVisibility For(ClaimsPrincipal user) => new(
            IncludeRawMaterials: IsInAny(user, AppRoleGroups.ProcurementOperators),
            IncludeFinishedProducts: IsInAny(user, RetailRoleGroups.RetailStockViewers));

        private static bool IsInAny(ClaimsPrincipal user, string csvRoles)
        {
            foreach (var role in csvRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (user.IsInRole(role))
                    return true;
            }

            return false;
        }
    }
}
