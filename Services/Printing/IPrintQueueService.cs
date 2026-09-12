using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Printing
{
    public interface IPrintQueueService
    {
        /// <summary>
        /// Enqueues a print job. If a job with the same IdempotencyKey already
        /// exists for this tenant, the call is a no-op and returns the existing id.
        /// This is what makes upstream retries safe: trigger the same event twice,
        /// the queue still only has one job.
        /// </summary>
        Task<Guid> EnqueueAsync(
            Guid tenantId,
            Guid orderId,
            Guid? kitchenId,
            Guid printerId,
            PrintJobType jobType,
            byte[] payload,
            string idempotencyKey,
            string? orderNumberSnapshot,
            string? kitchenNameSnapshot,
            CancellationToken ct,
            PrintPayloadType payloadType = PrintPayloadType.EscPos,
            bool isReprint = false);
    }
}
