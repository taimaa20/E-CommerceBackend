using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services;

public interface IOnlineShoppingService
{
    Task<OrderCreateDto> PrepareStoreOrderAsync(OrderCreateDto input, Guid tenantId, Guid branchId, CancellationToken ct);
    Task<OnlineMenuOrderContextDto> GetOrderContextAsync(Guid tenantId, Guid? branchId, CancellationToken ct);
    Task<Customer> ResolveCustomerAsync(
        Guid tenantId,
        string name,
        string phone,
        string? email,
        CancellationToken ct);
    Task<string?> ValidateAvailabilityAsync(
        Guid tenantId,
        Guid branchId,
        IReadOnlyList<OrderItemCreateDto> items,
        CancellationToken ct);
    Task<OnlineShoppingOrderStatusDto> GetOrderStatusAsync(
        Guid orderId,
        string clientOrderUuid,
        CancellationToken ct);
    Task<IReadOnlyList<OnlineShoppingProductAvailabilityDto>> GetProductAvailabilityAsync(
        Guid tenantId,
        Guid? branchId,
        CancellationToken ct);
    Task<OnlineStoreOrderLookupResultDto> LookupOrderAsync(
        string orderNumber,
        string contact,
        CancellationToken ct);
}
