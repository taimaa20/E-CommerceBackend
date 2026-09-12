using RestaurantPos.Api.DTOs.ExpenseInvoice;

namespace RestaurantPos.Api.DTOs
{
    /// <summary>
    /// Real-time KPIs for the shift summary dashboard. Window is
    /// [shift.OpenedAt, now). All fields are tenant-scoped, not per-cashier,
    /// so a single shift's dashboard reflects the whole branch state during
    /// the shift window.
    /// </summary>
    public class ShiftSummaryDto
    {
        public Guid     ShiftId      { get; set; }
        public DateTime OpenedAtUtc  { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public string   CashierName  { get; set; } = string.Empty;
        public int?     ShiftNumber  { get; set; }

        // KPIs
        public int     TotalOrders     { get; set; }
        public decimal TotalRevenue    { get; set; }
        public int     CompletedOrders { get; set; }
        public int     ServedOrders    { get; set; }
        public int     ReadyOrders     { get; set; }
        public int     PreparingOrders { get; set; }
        public int     PendingOrders   { get; set; }
        public int     PaidOrders      { get; set; }
        public int     CancelledOrders { get; set; }
        public int     RefundedOrders  { get; set; }

        // Channels
        public int     DeliveryOrders  { get; set; }
        public int     PartnerOrders   { get; set; }
        public int     TakeawayOrders  { get; set; }
        public int     DineInOrders    { get; set; }
        public int     QrOrders        { get; set; }

        // Documented expenses paid during the shift (cancelled ones excluded).
        public ShiftExpenseSummaryDto Expenses { get; set; } = new();

        // Close status
        public ShiftCloseValidationDto CloseStatus { get; set; } = new() { IsAllowed = true };
    }
}
