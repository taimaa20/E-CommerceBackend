using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public sealed record PaymentAdjustmentHistoryQuery(
        int Page,
        int PageSize,
        DateTime? DateFrom,
        DateTime? DateTo,
        string? User,
        string? Manager,
        Guid? PaymentMethodId,
        OrderType? OrderType,
        Guid? BranchId);

    public interface IPaymentAdjustmentRepository
    {
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
        Task LockOrderAsync(Guid orderId, CancellationToken ct);
        Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct);
        Task<User?> GetUserAsync(Guid userId, CancellationToken ct);
        Task<bool> HasPendingAsync(Guid orderId, CancellationToken ct);
        Task<PaymentAdjustment?> GetPendingAsync(Guid orderId, Guid adjustmentId, CancellationToken ct);
        Task<List<PaymentMethod>> GetActivePaymentMethodsAsync(CancellationToken ct);
        Task<IReadOnlyList<PaymentAdjustmentApproverDto>> GetApproversAsync(Guid requesterId, CancellationToken ct);
        Task SaveRequestAsync(PaymentAdjustment adjustment, CancellationToken ct);
        Task SaveApprovedAsync(
            Order order,
            IReadOnlyCollection<Payment> oldPayments,
            IReadOnlyCollection<Payment> newPayments,
            PaymentAdjustment adjustment,
            string paymentMethodSummary,
            DateTime approvedAt,
            CancellationToken ct);
        Task SaveDecisionAsync(CancellationToken ct);
        Task<PaginatedResponse<PaymentAdjustmentHistoryDto>> GetHistoryAsync(
            PaymentAdjustmentHistoryQuery query,
            CancellationToken ct);
    }
}
