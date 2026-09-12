using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories;

public interface IOnlineShoppingRepository
{
    Task<IReadOnlyList<Branch>> GetActiveBranchesAsync(Guid tenantId, CancellationToken ct);
    Task<Branch> ResolveActiveBranchAsync(Guid tenantId, Guid? branchId, CancellationToken ct);
    Task<IReadOnlyList<Product>> GetBranchProductsAsync(Guid tenantId, Guid branchId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, decimal>> GetBranchStockAsync(Guid branchId, CancellationToken ct);
    Task<IReadOnlyList<DeliveryZone>> GetDeliveryZonesAsync(Guid branchId, CancellationToken ct);
    Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(Guid branchId, CancellationToken ct);
    Task<Customer?> GetCustomerByPhoneAsync(string phone, CancellationToken ct);
    Task AddCustomerAsync(Customer customer, CancellationToken ct);
    Task<OnlineShoppingOrderRow?> GetOnlineOrderAsync(Guid orderId, string clientOrderUuid, CancellationToken ct);
    Task<IReadOnlyList<OnlineOrderLookupRow>> FindOnlineOrdersByNumberAsync(string orderNumber, CancellationToken ct);
}

/// Minimal projection used to verify guest ownership before any order data is
/// returned. Carries only what the check needs — never customer-facing content.
public sealed class OnlineOrderLookupRow
{
    public Guid OrderId { get; init; }
    public string ClientOrderUuid { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }
    public string? CustomerEmail { get; init; }
}

public sealed class OnlineShoppingOrderRow
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public Guid BranchId { get; init; }
    public string OrderType { get; init; } = string.Empty;
    public OrderStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public string? PaymentMethod { get; init; }
    public string? PaymentMethodName { get; init; }
    public string? PaymentMethodNameAr { get; init; }
    public string? SubmittedPaymentReference { get; init; }
    public Guid? LastPaymentId { get; init; }
    public string? LastPaymentClientActionId { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime? ScheduledFor { get; init; }
    public DateTime? DispatchedAt { get; init; }
    public bool AllItemsReady { get; init; }
    public bool IsRefunded { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string? DeliveryAddress { get; init; }
    public string? DeliveryNotes { get; init; }
    public string? DeliveryZoneName { get; init; }
    public decimal? DeliveryFee { get; init; }
    public OnlineShoppingBranchDto Branch { get; init; } = new();
    public IReadOnlyList<OnlineShoppingOrderItemDto> Items { get; init; } = [];
}
