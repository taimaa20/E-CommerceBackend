using Microsoft.EntityFrameworkCore;
using Npgsql;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Printing
{
    public class PrintQueueService : IPrintQueueService
    {
        // Postgres SQLSTATE for "unique_violation" — caught explicitly so a
        // race between two simultaneous enqueues with the same idempotency key
        // resolves cleanly instead of bubbling a 500.
        private const string UniqueViolationSqlState = "23505";

        private readonly PosDbContext _context;
        private readonly ILogger<PrintQueueService> _logger;

        public PrintQueueService(PosDbContext context, ILogger<PrintQueueService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Guid> EnqueueAsync(
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
            bool isReprint = false)
        {
            ArgumentNullException.ThrowIfNull(payload);
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));

            var branchId = await _context.Orders
                .AsNoTracking()
                .Where(o => o.TenantId == tenantId && o.Id == orderId)
                .Select(o => o.BranchId)
                .FirstOrDefaultAsync(ct);
            if (branchId == Guid.Empty)
                throw new InvalidOperationException($"Order {orderId} not found for print queue.");

            // First pre-check via the unique index. IgnoreQueryFilters because
            // a previously soft-deleted row with the same key would still
            // collide at INSERT time (the partial-filter is on DeletedAt only
            // for some indexes, not for ours).
            var existing = await _context.PrintJobs
                .IgnoreQueryFilters()
                .Where(j => j.TenantId == tenantId && j.IdempotencyKey == idempotencyKey)
                .Select(j => j.Id)
                .FirstOrDefaultAsync(ct);

            if (existing != Guid.Empty)
            {
                _logger.LogInformation(
                    "PrintJob with key {Key} already exists for tenant {Tenant}; skipping duplicate.",
                    idempotencyKey, tenantId);
                return existing;
            }

            var job = new PrintJob
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = branchId,
                OrderId = orderId,
                KitchenId = kitchenId,
                PrinterId = printerId,
                JobType = jobType,
                PayloadType = payloadType,
                IsReprint = isReprint,
                Status = PrintJobStatus.Pending,
                PayloadBase64 = Convert.ToBase64String(payload),
                IdempotencyKey = idempotencyKey,
                AttemptCount = 0,
                MaxAttempts = 3,
                NextAttemptAt = DateTime.UtcNow,
                OrderNumberSnapshot = orderNumberSnapshot,
                KitchenNameSnapshot = kitchenNameSnapshot
            };

            _context.PrintJobs.Add(job);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Race: another request inserted the same idempotency key
                // between our SELECT and INSERT. Detach the failed entity so the
                // DbContext stays usable, then resolve to the winner's id.
                _context.Entry(job).State = EntityState.Detached;

                var winnerId = await _context.PrintJobs
                    .IgnoreQueryFilters()
                    .Where(j => j.TenantId == tenantId && j.IdempotencyKey == idempotencyKey)
                    .Select(j => j.Id)
                    .FirstOrDefaultAsync(ct);

                if (winnerId == Guid.Empty)
                {
                    // Extremely unlikely (winner already deleted); rethrow so the
                    // caller can record this against its retry policy.
                    throw;
                }

                _logger.LogInformation(
                    "PrintJob enqueue race resolved for key {Key} → winner {WinnerId}.",
                    idempotencyKey, winnerId);
                return winnerId;
            }

            _logger.LogInformation(
                "PrintJob {JobId} enqueued: order={OrderId} type={Type} printer={Printer} bytes={Bytes}",
                job.Id, orderId, jobType, printerId, payload.Length);

            return job.Id;
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is PostgresException pg && pg.SqlState == UniqueViolationSqlState;
    }
}
