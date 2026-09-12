using Microsoft.AspNetCore.Http;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Security
{
    public static class AppRoleNames
    {
        public const string Admin = nameof(UserRole.Admin);
        public const string SuperAdmin = nameof(UserRole.SuperAdmin);
        public const string Manager = nameof(UserRole.Manager);
        public const string Waiter = nameof(UserRole.Waiter);
        public const string Kitchen = nameof(UserRole.Kitchen);
        public const string Cashier = nameof(UserRole.Cashier);
        public const string TrackerPickup = nameof(UserRole.TrackerPickup);
        public const string Owner = nameof(UserRole.Owner);
        public const string Marketing = nameof(UserRole.Marketing);
        public const string Customer = "Customer";
    }

    public static class AppRoleGroups
    {
        /// <summary>Admin only. Use when Manager must be excluded
        /// (e.g. opening another cashier's shift on their behalf).</summary>
        public const string AdminStrict = AppRoleNames.Admin;

        public const string AdminOnly = AppRoleNames.Admin + "," + AppRoleNames.Manager;
        public const string KitchenOperators = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Kitchen;
        public const string ProcurementOperators = AppRoleNames.Admin + "," + AppRoleNames.Kitchen;
        public const string CashierOperators = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Cashier;
        /// <summary>Admin or Cashier — opening/closing one's own shift, etc.
        /// Does NOT include Manager; use <see cref="CashierOperators"/> if Manager should also pass.</summary>
        public const string AdminOrCashier = AppRoleNames.Admin + "," + AppRoleNames.Cashier;
        public const string PosOrderEditors = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Waiter + "," + AppRoleNames.Cashier + "," + AppRoleNames.Kitchen;
        public const string TakeawayCheckoutOperators = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Waiter + "," + AppRoleNames.Cashier;
        public const string DineInTableOperators = AppRoleNames.Admin + "," + AppRoleNames.Waiter + "," + AppRoleNames.Cashier;
        public const string TrackerOperators = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + PosOrderEditors + "," + AppRoleNames.TrackerPickup;

        // Waste / Cancel feature
        /// <summary>Roles that may cancel an order (Admin, Manager, Waiter/Garçon, Cashier).</summary>
        public const string CancelOperators = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Waiter + "," + AppRoleNames.Cashier;

        /// <summary>Roles that may view waste log & cancel log.</summary>
        public const string WasteLogViewers = AppRoleNames.Admin + "," + AppRoleNames.Manager;

        /// <summary>Roles that may create manual waste requests.</summary>
        public const string WasteCreators = AppRoleNames.Admin + "," + AppRoleNames.Manager;

        /// <summary>Roles that may approve or reject manual waste requests.</summary>
        public const string WasteApprovers = AppRoleNames.Admin + "," + AppRoleNames.Manager;

        /// <summary>Roles that may issue post-payment refunds (Admin, Manager, Cashier — not Waiter).</summary>
        public const string RefundOperators = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Cashier;

        // Payment method adjustment capabilities. The application currently
        // maps named permissions to role groups; keeping capability names here
        // preserves one authorization surface if per-user grants are added later.
        public const string PaymentChangeRequesters = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Cashier;
        public const string PaymentChangeApprovers = AppRoleNames.Admin + "," + AppRoleNames.Manager;
        public const string PaymentAdjustmentViewers = AppRoleNames.Admin + "," + AppRoleNames.Manager;
        public const string PartnerDiscountOverrideManagers = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Owner;
        public const string DeliveryPartnerPriceAdjusters = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Cashier + "," + AppRoleNames.Owner;

        public const string AssetManagers = AppRoleNames.Admin + "," + AppRoleNames.Manager;
        public const string AssetViewers = AppRoleNames.Admin + "," + AppRoleNames.Manager + "," + AppRoleNames.Waiter + "," + AppRoleNames.Kitchen + "," + AppRoleNames.Cashier;

        // ── Order Numbering & Shift Management module ──────────────────────
        // Spec-mandated named permissions. Values intentionally alias existing
        // groups today; named so a future change ("let Manager configure shifts
        // but not numbering") only touches this file. Never inline these in
        // [Authorize] attributes — always reference by name.
        //
        // Note: there is no separate ForceShiftClose group. Per product spec
        // (no manager-approval flow), force close is gated by the per-tenant
        // ShiftRulesConfig.AllowForcedShiftClose toggle inside CloseShiftAsync,
        // not by role.
        public const string OrderNumberConfigManagers = AdminOnly;
        public const string ShiftConfigManagers       = AdminOnly;
        public const string ShiftOpenOperators        = CashierOperators;
        public const string ShiftCloseOperators       = CashierOperators;
        public const string ShiftDashboardViewers     = CashierOperators;
        public const string ConfigAuditViewers        = AdminOnly;

        public const string CostSharingViewers              = AdminOnly;
        public const string CostSharingManagers             = AdminOnly;
        public const string CostSharingOverrideOperators    = CashierOperators;
        public const string ProfitabilityViewers            = AdminOnly;
        public const string CostBreakdownViewers            = AdminOnly;
        public const string SettlementDetailViewers         = AdminOnly;
        public const string FinancialReportExporters        = AdminOnly;
        public const string PartnerFinancialViewers         = AdminOnly;
        public const string PaymentProviderFinancialViewers = AdminOnly;

        // ── Marketing role — read-only dashboards & analytics ──────────────
        // Each group is a strict superset of the group it replaced on the
        // target controller (existing roles preserved + Marketing appended),
        // so behavior for every other role is unchanged. Applied ONLY to
        // GET-only dashboard/analytics controllers — Marketing never reaches
        // an operational or write endpoint.
        public const string HomeDashboardViewers       = AdminOnly + "," + AppRoleNames.Marketing;
        public const string FinancialDashboardViewers  = AdminOnly + "," + AppRoleNames.Marketing;
        public const string OperationsDashboardViewers = PosOrderEditors + "," + AppRoleNames.Marketing;
        public const string CustomerAnalyticsViewers   = AdminOnly + "," + AppRoleNames.Marketing;
        public const string CustomerDirectoryViewers   = AdminOnly + "," + AppRoleNames.Marketing;
        public const string LoyaltyDashboardViewers    = AdminOnly + "," + AppRoleNames.Marketing;
    }

    public static class AppPermissionNames
    {
        public const string CanRequestPaymentChanges = nameof(CanRequestPaymentChanges);
        public const string CanApprovePaymentChanges = nameof(CanApprovePaymentChanges);
        public const string CanViewPaymentAdjustmentHistory = nameof(CanViewPaymentAdjustmentHistory);
        public const string CanAdjustDeliveryPartnerPrices = nameof(CanAdjustDeliveryPartnerPrices);
        public const string CanViewCostSharing = nameof(CanViewCostSharing);
        public const string CanManageCostSharing = nameof(CanManageCostSharing);
        public const string CanOverrideCostSharing = nameof(CanOverrideCostSharing);
        public const string CanViewProfitability = nameof(CanViewProfitability);
        public const string CanViewCostBreakdown = nameof(CanViewCostBreakdown);
        public const string CanViewSettlementDetails = nameof(CanViewSettlementDetails);
        public const string CanExportFinancialReports = nameof(CanExportFinancialReports);
        public const string CanViewPartnerFinancials = nameof(CanViewPartnerFinancials);
        public const string CanViewPaymentProviderFinancials = nameof(CanViewPaymentProviderFinancials);
    }

    public static class TrackerPickupAccessPolicy
    {
        public static bool IsAllowed(PathString path, string method)
        {
            var pathValue = path.Value ?? string.Empty;

            return (path.StartsWithSegments("/api/orders/tracker") && HttpMethods.IsGet(method))
                || (path.StartsWithSegments("/api/orders")
                    && HttpMethods.IsPut(method)
                    && (pathValue.EndsWith("/served", StringComparison.OrdinalIgnoreCase)
                        || pathValue.EndsWith("/pickup", StringComparison.OrdinalIgnoreCase)))
                || path.StartsWithSegments("/kitchenHub");
        }
    }
}
