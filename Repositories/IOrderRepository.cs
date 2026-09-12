using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IOrderRepository
    {
        Task<PaginatedResponse<OrderListItemDto>> GetOrdersAsync(OrderQueryFilter filter, bool isArabic, CancellationToken ct);
        Task<OrderListSummaryDto> GetOrdersSummaryAsync(OrderQueryFilter filter, CancellationToken ct);
        Task<List<OrderExportRowDto>> GetOrdersExportRowsAsync(OrderQueryFilter filter, CancellationToken ct);
        Task<bool> OrderExistsAsync(Guid orderId, Guid tenantId, Guid branchId, CancellationToken ct);
        Task<List<PartnerPriceOverrideAuditDto>> GetPartnerPriceOverrideAuditsAsync(Guid orderId, Guid tenantId, Guid branchId, CancellationToken ct);
        Task<OrderItem?> GetOrderItemForPartnerDiscountAsync(Guid orderId, Guid orderItemId, Guid tenantId, Guid branchId, CancellationToken ct);
        Task<Order?> GetOrderForDeliveryPartnerPriceAdjustmentAsync(Guid orderId, Guid tenantId, Guid branchId, CancellationToken ct);
        Task<Order?> GetOrderForTotalOverrideAsync(Guid orderId, Guid tenantId, Guid branchId, CancellationToken ct);
        Task<Order?> GetOrderForLifecycleAsync(Guid orderId, Guid tenantId, Guid branchId, CancellationToken ct);
        Task<User?> GetUserAsync(Guid userId, Guid tenantId, CancellationToken ct);
        Task AddPartnerPriceOverrideAuditAsync(PartnerPriceOverrideAudit audit, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }

    public sealed class OrderQueryFilter
    {
        public Guid TenantId { get; init; }
        public Guid? BranchId { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public OrderStatus? Status { get; init; }
        public string? Payment { get; init; }
        public OrderType? OrderType { get; init; }
        public OrderSource? OrderSource { get; init; }
        public Guid? PartnerId { get; init; }
        public bool? VoucherApplied { get; init; }
        public DateTime? DateFrom { get; init; }
        public DateTime? DateTo { get; init; }
        public string? Search { get; init; }
        public bool SortDescending { get; init; } = true;
    }
}
