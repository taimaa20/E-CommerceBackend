using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Helpers;

public static class OnlineOrderStatusMapper
{
    public const string OrderPlaced = "OrderPlaced";
    public const string Confirmed = "Confirmed";
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string ReadyForPickup = "ReadyForPickup";
    public const string OutForDelivery = "OutForDelivery";
    public const string Delivered = "Delivered";
    public const string PickedUp = "PickedUp";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";
    public const string PaymentFailed = "PaymentFailed";

    public static string GetCustomerStatus(
        OrderStatus status,
        OrderType orderType,
        bool allItemsReady,
        bool isRefunded,
        DateTime? dispatchedAt)
    {
        if (isRefunded)
            return Refunded;
        if (status == OrderStatus.PaymentCancelled)
            return PaymentFailed;
        if (status == OrderStatus.Cancelled)
            return Cancelled;
        if (status == OrderStatus.Completed)
            return Completed;
        if (status == OrderStatus.Served)
            return orderType == OrderType.Delivery ? Delivered : PickedUp;
        if (orderType == OrderType.Delivery && dispatchedAt.HasValue)
            return OutForDelivery;
        if (status == OrderStatus.Ready || allItemsReady)
            return orderType == OrderType.Delivery ? Ready : ReadyForPickup;
        if (status == OrderStatus.Preparing)
            return Preparing;
        if (status == OrderStatus.Paid)
            return Confirmed;
        return status == OrderStatus.PendingPayment ? OrderPlaced : Confirmed;
    }
}
