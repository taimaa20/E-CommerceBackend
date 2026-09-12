using Microsoft.Extensions.Options;
using RestaurantPos.Api.DTOs;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Services.Printing.Agent.Writers;

namespace RestaurantPos.Api.Services.Printing.Agent
{
    /// <summary>
    /// Polls the cloud's <c>/api/printing/agent/jobs/pending</c> endpoint,
    /// claims one job at a time (atomic Pending→Printing transition), prints
    /// it via the appropriate writer, and reports the outcome.
    ///
    /// Registered ONLY when <c>PrintAgent:Enabled = true</c> in config —
    /// which is never the case on the Azure App Service deployment. On
    /// Azure, this class is dead code: it occupies disk space inside the
    /// publish bundle and nothing else.
    ///
    /// On the in-store Windows PC running the same publish artifact with
    /// <c>PrintAgent__Enabled=true</c>, this is the entire job: poll, print,
    /// report.
    /// </summary>
    public sealed class LocalPrintAgentBackgroundService : BackgroundService
    {
        private const int PrinterTypeNetwork = 0;
        private const int PrinterTypeUsb = 1;

        private readonly AgentApiClient _api;
        private readonly AgentNetworkWriter _network;
        private readonly AgentWindowsSpoolerWriter _spooler;
        private readonly AgentHtmlWriter _htmlWriter;
        private readonly PrintAgentSettings _settings;
        private readonly ILogger<LocalPrintAgentBackgroundService> _logger;

        public LocalPrintAgentBackgroundService(
            AgentApiClient api,
            AgentNetworkWriter network,
            AgentWindowsSpoolerWriter spooler,
            AgentHtmlWriter htmlWriter,
            IOptions<PrintAgentSettings> settings,
            ILogger<LocalPrintAgentBackgroundService> logger)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _spooler = spooler ?? throw new ArgumentNullException(nameof(spooler));
            _htmlWriter = htmlWriter ?? throw new ArgumentNullException(nameof(htmlWriter));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var poll = TimeSpan.FromSeconds(Math.Max(1, _settings.PollIntervalSeconds));
            _logger.LogInformation(
                "[PrintAgent] Started — base={Base}, tenant={Tenant}, poll={Poll}s, batch={Batch}",
                _settings.BaseUrl, _settings.TenantId, poll.TotalSeconds, _settings.BatchSize);

            using var timer = new PeriodicTimer(poll);
            await TryRunBatchAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TryRunBatchAsync(stoppingToken);
            }
        }

        private async Task TryRunBatchAsync(CancellationToken ct)
        {
            try
            {
                var jobs = await _api.GetPendingJobsAsync(_settings.BatchSize, ct);
                if (jobs.Count == 0) return;

                foreach (var job in jobs)
                {
                    if (ct.IsCancellationRequested) break;
                    await ProcessJobAsync(job, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PrintAgent] Batch tick failed; will retry next interval");
            }
        }

        private async Task ProcessJobAsync(AgentJobDto job, CancellationToken ct)
        {
            // Atomic claim — if a sibling (or another tick) beat us, skip silently.
            if (!await _api.ClaimAsync(job.Id, ct))
            {
                _logger.LogDebug("[PrintAgent] Job {JobId} already claimed elsewhere", job.Id);
                return;
            }

            try
            {
                var payload = Convert.FromBase64String(job.PayloadBase64);

                if (job.PayloadType == (int)PrintPayloadType.Html)
                {
                    // Receipt HTML → render via Puppeteer sidecar
                    var success = await _htmlWriter.PrintAsync(job, payload, ct);
                    await _api.ReportResultAsync(
                        job.Id,
                        success,
                        success ? null : "HTML receipt sidecar failed",
                        ct);
                    return;
                }

                IAgentPrinterWriter writer = job.PrinterType switch
                {
                    PrinterTypeNetwork => _network,
                    PrinterTypeUsb => _spooler,
                    _ => throw new InvalidOperationException($"Unknown printer type {job.PrinterType}")
                };

                await writer.WriteAsync(job, payload, ct);
                await _api.ReportResultAsync(job.Id, success: true, error: null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[PrintAgent] Print failed: order {Order} on {Printer} (attempt {Attempt}/{Max})",
                    job.OrderNumber, job.PrinterName, job.AttemptCount, job.MaxAttempts);

                try
                {
                    await _api.ReportResultAsync(job.Id, success: false, error: ex.Message, ct);
                }
                catch (Exception reportEx)
                {
                    _logger.LogError(reportEx, "[PrintAgent] Failed to report result for {JobId}", job.Id);
                }
            }
        }
    }
}
