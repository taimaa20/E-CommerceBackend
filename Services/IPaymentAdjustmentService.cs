using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface IPaymentAdjustmentService
    {
        Task<PaymentAdjustmentResultDto> RequestAsync(
            Guid orderId,
            PaymentAdjustmentCreateDto request,
            Guid requesterId,
            UserRole requesterRole,
            CancellationToken ct);
        Task<PaymentAdjustmentReviewDto> GetPendingAsync(
            Guid orderId,
            Guid adjustmentId,
            CancellationToken ct);
        Task<PaymentAdjustmentResultDto> ApproveAsync(
            Guid orderId,
            Guid adjustmentId,
            PaymentAdjustmentDecisionDto request,
            Guid approverId,
            UserRole approverRole,
            CancellationToken ct);
        Task RejectAsync(
            Guid orderId,
            Guid adjustmentId,
            PaymentAdjustmentDecisionDto request,
            Guid approverId,
            UserRole approverRole,
            CancellationToken ct);
        Task<IReadOnlyList<PaymentAdjustmentApproverDto>> GetApproversAsync(
            Guid requesterId,
            CancellationToken ct);
        Task<PaginatedResponse<PaymentAdjustmentHistoryDto>> GetHistoryAsync(
            PaymentAdjustmentReportFilterDto filter,
            CancellationToken ct);
    }
}
