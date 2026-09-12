using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface ICashierShiftRepository
    {
        Task<List<CashierBalanceShift>> GetShiftsAsync(
            Guid tenantId,
            Guid branchId,
            CashierShiftQueryDto query,
            bool isAdmin,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<List<CashierShiftOwnerOptionDto>> GetShiftOwnerOptionsAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default);

        Task<CashierBalanceShift?> GetShiftByIdAsync(
            Guid tenantId,
            Guid branchId,
            Guid shiftId,
            CancellationToken cancellationToken = default);

        Task<CashierBalanceShift?> GetActiveShiftAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            CancellationToken cancellationToken = default);

        Task<CashierBalanceShift?> GetShiftByOpenTransactionIdAsync(
            Guid tenantId,
            Guid branchId,
            string clientActionId,
            CancellationToken cancellationToken = default);

        Task<User?> GetCashierByIdAsync(
            Guid tenantId,
            Guid cashierId,
            CancellationToken cancellationToken = default);

        Task<bool> HasActiveShiftAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            CancellationToken cancellationToken = default);

        /// <summary>True if ANY open shift exists for the tenant. Used by
        /// ActiveShiftRule = SinglePerBranch (single-shift-per-tenant scope).</summary>
        Task<bool> HasAnyActiveShiftAsync(
            Guid tenantId,
            Guid branchId,
            CancellationToken cancellationToken = default);

        /// <summary>True if an open shift exists for the given POS device.
        /// Used by ActiveShiftRule = SinglePerPosDevice.</summary>
        Task<bool> HasActiveShiftForDeviceAsync(
            Guid tenantId,
            Guid branchId,
            string posDeviceId,
            CancellationToken cancellationToken = default);

        /// <summary>Counts per status used by the close-validator. Window is
        /// [shift.OpenedAt, now). Scoped to the cashier who owns the shift — an
        /// order is "the cashier's" when WaiterId or PaidByUserId equals
        /// <paramref name="cashierId"/>. This avoids a cashier's shift close being
        /// blocked by a colleague's open orders.</summary>
        Task<ShiftValidationCounts> GetShiftValidationCountsAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            CancellationToken cancellationToken = default);

        /// <summary>Aggregate counts + revenue for the Shift Summary Dashboard.
        /// Same per-cashier scoping rule as <see cref="GetShiftValidationCountsAsync"/>.</summary>
        Task<ShiftDashboardCounts> GetShiftDashboardCountsAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            CancellationToken cancellationToken = default);

        Task<int> GetNextShiftNumberAsync(
            Guid tenantId,
            Guid branchId,
            Guid cashierId,
            CancellationToken cancellationToken = default);

        Task<SystemSettings?> GetSystemSettingsAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default);

        Task<List<CashierShiftLiveMetrics>> GetLiveMetricsAsync(
            Guid tenantId,
            Guid branchId,
            IReadOnlyCollection<Guid> shiftIds,
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default);

        /// <summary>Documented expenses recorded against the given shifts.
        /// Cancelled rows are excluded — they no longer affect any total.</summary>
        Task<Dictionary<Guid, ShiftExpenseAmounts>> GetExpenseAmountsAsync(
            Guid tenantId,
            Guid branchId,
            IReadOnlyCollection<Guid> shiftIds,
            CancellationToken cancellationToken = default);

        Task AddAsync(CashierBalanceShift shift, CancellationToken cancellationToken = default);
        void Remove(CashierBalanceShift shift);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }

    public sealed class ShiftDashboardCounts
    {
        public int TotalOrders     { get; init; }
        public decimal TotalRevenue { get; init; }
        public int CompletedOrders { get; init; }
        public int ServedOrders    { get; init; }
        public int ReadyOrders     { get; init; }
        public int PreparingOrders { get; init; }
        public int PendingOrders   { get; init; }
        public int PaidOrders      { get; init; }
        public int CancelledOrders { get; init; }
        public int RefundedOrders  { get; init; }
        public int DeliveryOrders  { get; init; }
        public int PartnerOrders   { get; init; }
        public int TakeawayOrders  { get; init; }
        public int DineInOrders    { get; init; }
        public int QrOrders        { get; init; }
    }

    public sealed class ShiftValidationCounts
    {
        // Per-status counts — used by the "Allow {Status}" gates in the validator.
        public int NewCount                  { get; init; }
        public int PreparingCount            { get; init; }
        public int ReadyCount                { get; init; }
        public int ServedCount               { get; init; }
        public int PaidCount                 { get; init; }

        public int PendingDeliveryCount      { get; init; }
        public int PendingCancellationCount  { get; init; }

        // ── Semantic categories used by the "RequireAllOrders___" gates ─────
        // Use PaidAt (financial settlement timestamp) rather than Status==Paid
        // because an order may legitimately sit at Status=Served while already
        // being paid for. The popular UI badges ("مدفوع" / "تم التقديم") reflect
        // these independent dimensions, and the validator must match them.

        /// <summary>Orders not yet financially settled. PaidAt is null and the
        /// order isn't cancelled.</summary>
        public int UnpaidCount               { get; init; }

        /// <summary>Orders whose kitchen workflow hasn't reached Ready — i.e.
        /// Status in (New, Preparing).</summary>
        public int NotReadyCount             { get; init; }

        /// <summary>Orders not yet handed to the customer — Status in
        /// (New, Preparing, Ready).</summary>
        public int NotServedCount            { get; init; }

        /// <summary>Orders that are neither financially settled nor explicitly
        /// terminal — PaidAt is null AND Status not in (Completed, Cancelled).</summary>
        public int NotCompletedCount         { get; init; }
    }

    /// <summary>
    /// Documented shift-expense aggregates. <see cref="CashExpensesTotal"/> is the
    /// only part that leaves the physical drawer, so it is the only part that
    /// reduces expected cash.
    /// </summary>
    public sealed class ShiftExpenseAmounts
    {
        public static readonly ShiftExpenseAmounts Empty = new();

        public int ExpenseCount { get; init; }
        public decimal ExpensesTotal { get; init; }
        public decimal CashExpensesTotal { get; init; }
        public decimal NonCashExpensesTotal { get; init; }
    }

    public sealed class CashierShiftLiveMetrics
    {
        public Guid ShiftId { get; init; }
        public int PaidOrderCount { get; init; }
        public decimal OrdersTotal { get; init; }
        public decimal CashOrdersTotal { get; init; }
        public decimal CardOrdersTotal { get; init; }
        public decimal RefundsTotal { get; init; }
        public decimal ExpectedBalance { get; init; }
        public decimal FoodRevenue { get; init; }
        public decimal DeliveryCollected { get; init; }
        public decimal DeliveryCost { get; init; }
        public decimal DeliveryProfit { get; init; }
        public decimal MarketplaceFees { get; init; }
        public decimal MarketplaceServiceFees { get; init; }
        public decimal NetRestaurantRevenue { get; init; }
        public int ExpenseCount { get; init; }
        public decimal ExpensesTotal { get; init; }
        public decimal CashExpensesTotal { get; init; }
        public decimal NonCashExpensesTotal { get; init; }
        public List<CashierShiftPartnerSalesDto> PartnerSalesBreakdown { get; init; } = new();
        public List<CashierShiftPaymentMethodBreakdownDto> PaymentMethodsBreakdown { get; init; } = new();
    }
}
