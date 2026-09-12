namespace RestaurantPos.Api.Hubs
{
    /// <summary>
    /// Constants for SignalR event names broadcast over <see cref="KitchenHub"/>.
    ///
    /// Why constants and not magic strings:
    ///  - Renames are compile-checked, not runtime breakage.
    ///  - Frontend listeners (cashier, kitchen, bar, tracker, KDS) bind to these exact strings;
    ///    a typo on either side means a silent feature drop. Constants make typos impossible.
    ///  - One canonical place to grep when adding a new listener or auditing payload shape.
    ///
    /// Frontend mirror: keep `POS-Frontend/lib/signalr-events.ts` in sync if you add/rename here.
    /// </summary>
    public static class KitchenHubEvents
    {
        // ── Order lifecycle ────────────────────────────────────────────────
        /// <summary>New order created or existing order touched. Payload: full OrderDto.</summary>
        public const string ReceiveNewOrder = "ReceiveNewOrder";

        /// <summary>Items appended to an existing order. Payload: { orderId, items[] }.</summary>
        public const string NewItemsAdded = "NewItemsAdded";

        /// <summary>Order paid. Payload: { orderId, orderNumber, tableName, totalAmount, paymentMethod }.</summary>
        public const string OrderPaid = "OrderPaid";

        /// <summary>Order cancelled (admin/manager confirmed). Payload: { orderId, reason }.</summary>
        public const string OrderCancelled = "OrderCancelled";

        /// <summary>Order refunded (full or partial). Payload: { orderId, refundedAmount }.</summary>
        public const string OrderRefunded = "OrderRefunded";

        // ── Kitchen / KDS ──────────────────────────────────────────────────
        /// <summary>A single order item is ready (kitchen marked it complete). Payload: { orderId, orderItemId }.</summary>
        public const string ItemReady = "ItemReady";

        /// <summary>All items in an order are ready. Payload: { orderId, orderNumber, tableName }.</summary>
        public const string OrderReady = "OrderReady";

        /// <summary>Order served to the customer. Payload: { orderId }.</summary>
        public const string OrderServed = "OrderServed";

        /// <summary>Takeaway/pickup order picked up. Payload: { orderId }.</summary>
        public const string OrderPickedUp = "OrderPickedUp";

        // ── Cancellation flow (kitchen-driven approval) ────────────────────
        /// <summary>Cashier requested a cancel; kitchen must approve. Payload: { orderId, reason }.</summary>
        public const string CancelRequested = "CancelRequested";

        /// <summary>Cancel request resolved (approved or rejected). Payload: { orderId, approved }.</summary>
        public const string CancellationResolved = "CancellationResolved";

        // ── Cashier shift ──────────────────────────────────────────────────
        /// <summary>Cashier shift opened, closed, or balance changed. Payload: { shiftId, status, ... }.</summary>
        public const string CashierShiftUpdated = "CashierShiftUpdated";

        // ── Notifications ──────────────────────────────────────────────────
        /// <summary>Generic notification broadcast. Payload: NotificationDto.</summary>
        public const string ReceiveNotification = "ReceiveNotification";

        // ── Generic / chat (kept for backward compatibility) ───────────────
        /// <summary>Generic chat-style message used by KitchenHub.SendMessage. Payload: (user, message).</summary>
        public const string ReceiveMessage = "ReceiveMessage";
    }
}
