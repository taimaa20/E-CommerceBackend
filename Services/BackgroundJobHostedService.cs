using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantPos.Api.Services
{
    public class BackgroundJobHostedService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

        private readonly IReadOnlyList<IBackgroundJob> _jobs;
        private readonly ILogger<BackgroundJobHostedService> _logger;

        public BackgroundJobHostedService(
            IEnumerable<IBackgroundJob> jobs,
            ILogger<BackgroundJobHostedService> logger)
        {
            _jobs = jobs?.ToArray() ?? throw new ArgumentNullException(nameof(jobs));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[BackgroundJobs] Runner started with {JobCount} job(s).", _jobs.Count);

            await RunJobsAsync(stoppingToken);

            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunJobsAsync(stoppingToken);
            }
        }

        private async Task RunJobsAsync(CancellationToken cancellationToken)
        {
            foreach (var job in _jobs)
            {
                try
                {
                    await job.RunIfDueAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BackgroundJobs] Job {JobName} failed.", job.JobName);
                }
            }
        }
    }
}
