using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface IVoucherService
    {
        Task<VoucherAvailabilityDto> GetAvailabilityAsync(Guid tenantId, CancellationToken ct);
        Task<VoucherAuditSummaryDto> GetTodayAuditAsync(Guid tenantId, CancellationToken ct);
        Task<VoucherApplicationResult> ApplyToOrderAsync(Order order, Guid? userId, decimal currentTotal, CancellationToken ct);
    }

    public sealed class VoucherApplicationResult
    {
        public decimal DiscountAmount { get; init; }
        public decimal FinalTotal { get; init; }
        public int RemainingToday { get; init; }
        public VoucherUsageAudit Audit { get; init; } = new();
    }
}
