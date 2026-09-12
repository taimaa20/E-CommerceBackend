using Microsoft.EntityFrameworkCore.Storage;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public interface IPaymentRepository
    {
        Task<Order?> GetOrderForPaymentAsync(Guid orderId, CancellationToken cancellationToken = default);
        Task<Order?> GetOrderForPaymentIntegrationAsync(Guid orderId, CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task<List<PaymentMethod>> GetActivePaymentMethodsAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<int> UpdateOrderPaymentFieldsAsync(Order order, CancellationToken cancellationToken = default);
        Task AddPaymentRecordAsync(Payment payment, CancellationToken cancellationToken = default);
        Task InsertPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
        Task<int> UpdateTableStatusAsync(Guid tableId, TableStatus status, CancellationToken cancellationToken = default);
        /// <summary>
        /// Acquires a PostgreSQL row-level lock on the Orders row for the
        /// given orderId until the surrounding transaction commits / rolls
        /// back. Used by the payment flow to serialize concurrent payment
        /// transactions for the same order so the overpayment guard can
        /// rely on a stable view of committed payments.
        /// </summary>
        Task LockOrderRowAsync(Guid orderId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Sum of payment amounts currently committed for the order. Caller
        /// MUST be inside a transaction that has already called
        /// LockOrderRowAsync, otherwise the result is racy.
        /// </summary>
        Task<decimal> GetTotalPaidAmountAsync(Guid orderId, CancellationToken cancellationToken = default);
    }
}
