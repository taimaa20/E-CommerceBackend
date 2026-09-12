using RestaurantPos.Api.DTOs;

namespace RestaurantPos.Api.Services;

public interface ISimplePurchaseService
{
    Task<SimplePurchaseDetailsDto> CreateAsync(
        SimplePurchaseCreateDto dto,
        Guid? userId,
        string? userName,
        bool isArabic,
        CancellationToken ct);

    Task<SimplePurchaseDetailsDto> GetDetailsAsync(
        Guid purchaseOrderId,
        bool isArabic,
        CancellationToken ct);

    Task<SimplePurchaseDetailsDto> ReceiveAsync(
        Guid purchaseOrderId,
        bool isArabic,
        CancellationToken ct);

    Task<SimplePurchaseDetailsDto> ApproveAsync(
        Guid purchaseOrderId,
        bool isArabic,
        CancellationToken ct);
}
