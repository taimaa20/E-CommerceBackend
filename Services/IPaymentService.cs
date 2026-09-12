using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public interface IPaymentService
    {
        Task<PaymentProcessResult> AddPaymentAsync(
            CreatePaymentRequest request,
            Guid? userId,
            UserRole userRole,
            CancellationToken cancellationToken = default);

        Task<PaymentProcessResult> AddOnlinePaymentAsync(
            CreatePaymentRequest request,
            Guid branchId,
            CancellationToken cancellationToken = default);
    }

    public sealed class PaymentProcessResult
    {
        public Order Order { get; init; } = null!;
        public Payment Payment { get; init; } = null!;
        public decimal PaidAmount { get; init; }
        public decimal RemainingAmount { get; init; }
        public string PaymentStatus { get; init; } = "Pending";
        public bool BecamePaid { get; init; }
        public string? PaymentMethodSummary { get; init; }
        public int? VoucherRemainingToday { get; init; }
    }
}
