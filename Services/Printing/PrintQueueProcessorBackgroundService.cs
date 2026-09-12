using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services.Printing
{
    /// <summary>
    /// Continuously pulls due PrintJobs (Pending OR Failed-with-retry-due) and
    /// sends them to the configured printer. Each tick:
    ///   1. Pick up to <see cref="BatchSize"/> due jobs (oldest first) bypassing
    ///      tenant filter — this is a system-level worker.
    ///   2. Mark each as Printing, then attempt the network send.
    ///   3. On success → Completed. On failure → Failed with exponential backoff,
    ///      or DeadLetter when MaxAttempts is exhausted.
    ///
    /// The poll interval is short (2s) because tickets must reach the line in
    /// real time. The batch size keeps a single tick bounded.
    /// </summary>
    public class PrintQueueProcessorBackgroundService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
        // A job stuck in Printing for longer than this is assumed to be the
        // ghost of a crashed worker. Larger than the printer connect+write
        // timeout (3s + 5s = 8s) plus a safety margin.
        private static readonly TimeSpan StalePrintingThreshold = TimeSpan.FromMinutes(2);
        private const int BatchSize = 20;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PrintQueueProcessorBackgroundService> _logger;

        public PrintQueueProcessorBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<PrintQueueProcessorBackgroundService> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[PrintQueue] Processor started (poll={Poll}s, batch={Batch}).",
                PollInterval.TotalSeconds, BatchSize);

            // Crash-recovery: any job left in Printing from a previous process
            // instance is reset to Pending so it can be picked up on first tick.
            await RecoverStalePrintingJobsAsync(stoppingToken);

            using var timer = new PeriodicTimer(PollInterval);

            // First tick immediately, then on schedule.
            await TryRunBatchAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested
                && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TryRunBatchAsync(stoppingToken);
            }
        }

        private async Task RecoverStalePrintingJobsAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<PosDbContext>();
                var cutoff = DateTime.UtcNow - StalePrintingThreshold;

                // Bulk reset: bypass the tenant filter, leave AttemptCount alone
                // (the attempt was real — this is a re-pick, not a re-attempt).
                var resetCount = await context.PrintJobs
                    .IgnoreQueryFilters()
                    .Where(j => j.DeletedAt == null
                                && j.Status == PrintJobStatus.Printing
                                && (j.LastAttemptAt == null || j.LastAttemptAt <= cutoff))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(j => j.Status, PrintJobStatus.Pending)
                        .SetProperty(j => j.NextAttemptAt, DateTime.UtcNow), ct);

                if (resetCount > 0)
                {
                    _logger.LogWarning(
                        "[PrintQueue] Recovered {Count} job(s) stuck in Printing from a previous run.",
                        resetCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PrintQueue] Stale-Printing recovery failed; processor continues.");
            }
        }

        private async Task TryRunBatchAsync(CancellationToken ct)
        {
            try
            {
                await RunBatchAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PrintQueue] Tick failed.");
            }
        }

        private async Task RunBatchAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var network = scope.ServiceProvider.GetRequiredService<INetworkPrinterClient>();

            var now = DateTime.UtcNow;

            // IgnoreQueryFilters: this is a system worker that crosses tenants.
            // We still write tenant ids on every job; the SignalR/HTTP layer is
            // tenant-scoped — only the worker bypasses the filter.
            //
            // The Network filter on the JOIN is critical: USB-typed printers
            // are served by an external local agent (separate process running
            // on the cashier PC). We must NOT pick those jobs up here — every
            // attempt would fail at NetworkPrinterClient and DeadLetter the row
            // even though a perfectly fine agent is about to handle it.
            // USB jobs sit Pending until an agent calls /api/printing/agent/...
            // HTML receipts also wait for the agent sidecar, because raw HTML
            // is not a printer-ready ESC/POS payload.
            var due = await context.PrintJobs
                .IgnoreQueryFilters()
                .Join(context.Printers.IgnoreQueryFilters(),
                    j => j.PrinterId,
                    p => p.Id,
                    (j, p) => new { Job = j, PrinterType = p.Type, UseLocalAgent = p.UseLocalAgent })
                .Where(x => x.Job.DeletedAt == null
                    && x.PrinterType == PrinterType.Network
                    && !x.UseLocalAgent
                    && x.Job.PayloadType != PrintPayloadType.Html
                    && (x.Job.Status == PrintJobStatus.Pending
                        || (x.Job.Status == PrintJobStatus.Failed
                            && x.Job.AttemptCount < x.Job.MaxAttempts
                            && (x.Job.NextAttemptAt == null || x.Job.NextAttemptAt <= now))))
                .OrderBy(x => x.Job.NextAttemptAt ?? x.Job.CreatedAt)
                .Take(BatchSize)
                .Select(x => x.Job)
                .ToListAsync(ct);

            if (due.Count == 0) return;

            foreach (var job in due)
            {
                await ProcessJobAsync(context, network, job, ct);
            }
        }

        private async Task ProcessJobAsync(PosDbContext context, INetworkPrinterClient network, PrintJob job, CancellationToken ct)
        {
            // Mark as Printing first so a crashed worker doesn't repick the same row.
            job.Status = PrintJobStatus.Printing;
            job.AttemptCount++;
            job.LastAttemptAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            var printer = await context.Printers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == job.PrinterId && p.DeletedAt == null, ct);

            if (printer == null)
            {
                await MarkFailureAsync(context, job, $"Printer {job.PrinterId} not found.", ct);
                return;
            }

            if (!printer.IsActive)
            {
                await MarkFailureAsync(context, job, $"Printer {printer.Name} is inactive.", ct);
                return;
            }

            try
            {
                var payload = Convert.FromBase64String(job.PayloadBase64);
                await network.SendAsync(printer, payload, ct);

                job.Status = PrintJobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.LastError = null;
                await context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "[PrintQueue] Job {JobId} completed (order={Order} type={Type} printer={Printer}).",
                    job.Id, job.OrderNumberSnapshot, job.JobType, printer.Name);
            }
            catch (Exception ex)
            {
                await MarkFailureAsync(context, job, ex.Message, ct);
                _logger.LogWarning(ex,
                    "[PrintQueue] Job {JobId} attempt {Attempt}/{Max} failed against {Printer}.",
                    job.Id, job.AttemptCount, job.MaxAttempts, printer.Name);
            }
        }

        private static async Task MarkFailureAsync(PosDbContext context, PrintJob job, string error, CancellationToken ct)
        {
            job.LastError = error;

            if (job.AttemptCount >= job.MaxAttempts)
            {
                job.Status = PrintJobStatus.DeadLetter;
                job.NextAttemptAt = null;
            }
            else
            {
                job.Status = PrintJobStatus.Failed;
                // Exponential backoff with jitter: 5s, 30s, 2min — caps below
                // the 5-minute default so a transient network blip recovers fast.
                var backoffSeconds = job.AttemptCount switch
                {
                    1 => 5,
                    2 => 30,
                    _ => 120
                };
                var jitterMs = Random.Shared.Next(0, 1500);
                job.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoffSeconds).AddMilliseconds(jitterMs);
            }

            await context.SaveChangesAsync(ct);
        }
    }
}
